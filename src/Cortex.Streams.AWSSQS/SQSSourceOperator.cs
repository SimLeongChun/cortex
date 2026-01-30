using Amazon.SQS.Model;
using Amazon.SQS;
using Cortex.Streams.Operators;
using System.Threading.Tasks;
using System.Threading;
using System;
using Cortex.Streams.AWSSQS.Deserializers;
using Amazon;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Streams.AWSSQS
{
    /// <summary>
    /// AWS SQS Source Operator with deserialization support, credential injection, and visibility timeout configuration.
    /// </summary>
    /// <typeparam name="TOutput">The type of objects to emit.</typeparam>
    public class SQSSourceOperator<TOutput> : ISourceOperator<TOutput>, IDisposable
    {
        private readonly string _queueUrl;
        private readonly IAmazonSQS _sqsClient;
        private readonly IDeserializer<TOutput> _deserializer;
        private readonly ILogger<SQSSourceOperator<TOutput>> _logger;
        private readonly Action<Exception, Message> _errorHandler;
        private readonly bool _deleteAfterProcess;
        private readonly int _visibilityTimeout;
        private readonly int _maxNumberOfMessages;
        private readonly int _waitTimeSeconds;
        private readonly int _maxRetries;
        private readonly TimeSpan _retryDelay;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _pollingTask;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SQSSourceOperator{TOutput}"/> class.
        /// </summary>
        /// <param name="queueUrl">The SQS queue URL.</param>
        /// <param name="deserializer">The deserializer to convert message bodies to TOutput objects.</param>
        /// <param name="region">The AWS region. Default is us-east-1.</param>
        /// <param name="credentials">Optional AWS credentials. If null, uses default credential chain.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="deleteAfterProcess">Whether to delete messages after successful processing. Default is true.</param>
        /// <param name="visibilityTimeout">The visibility timeout in seconds. Default is 30.</param>
        /// <param name="maxNumberOfMessages">Maximum number of messages to receive per poll. Default is 10.</param>
        /// <param name="waitTimeSeconds">Long polling wait time in seconds. Default is 20.</param>
        /// <param name="errorHandler">Optional error handler for processing failures.</param>
        /// <param name="maxRetries">Maximum number of retry attempts for failed operations. Default is 3.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between retries. Default is 1000ms.</param>
        public SQSSourceOperator(
            string queueUrl,
            IDeserializer<TOutput> deserializer = null,
            RegionEndpoint region = null,
            AWSCredentials credentials = null,
            ILogger<SQSSourceOperator<TOutput>> logger = null,
            bool deleteAfterProcess = true,
            int visibilityTimeout = 30,
            int maxNumberOfMessages = 10,
            int waitTimeSeconds = 20,
            Action<Exception, Message> errorHandler = null,
            int maxRetries = 3,
            int retryDelayMs = 1000)
        {
            _queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));
            _deserializer = deserializer ?? new DefaultJsonDeserializer<TOutput>();
            _logger = logger ?? NullLogger<SQSSourceOperator<TOutput>>.Instance;
            _deleteAfterProcess = deleteAfterProcess;
            _visibilityTimeout = visibilityTimeout;
            _maxNumberOfMessages = Math.Min(maxNumberOfMessages, 10); // SQS max is 10
            _waitTimeSeconds = Math.Min(waitTimeSeconds, 20); // SQS max is 20
            _errorHandler = errorHandler;
            _maxRetries = maxRetries;
            _retryDelay = TimeSpan.FromMilliseconds(retryDelayMs);

            var config = new AmazonSQSConfig
            {
                RegionEndpoint = region ?? RegionEndpoint.USEast1,
                MaxErrorRetry = 3,
                Timeout = TimeSpan.FromSeconds(60)
            };

            _sqsClient = credentials != null
                ? new AmazonSQSClient(credentials, config)
                : new AmazonSQSClient(config);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SQSSourceOperator{TOutput}"/> class with an existing SQS client.
        /// </summary>
        /// <param name="queueUrl">The SQS queue URL.</param>
        /// <param name="sqsClient">An existing IAmazonSQS client instance.</param>
        /// <param name="deserializer">The deserializer to convert message bodies to TOutput objects.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="deleteAfterProcess">Whether to delete messages after successful processing. Default is true.</param>
        /// <param name="visibilityTimeout">The visibility timeout in seconds. Default is 30.</param>
        /// <param name="maxNumberOfMessages">Maximum number of messages to receive per poll. Default is 10.</param>
        /// <param name="waitTimeSeconds">Long polling wait time in seconds. Default is 20.</param>
        /// <param name="errorHandler">Optional error handler for processing failures.</param>
        /// <param name="maxRetries">Maximum number of retry attempts for failed operations. Default is 3.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between retries. Default is 1000ms.</param>
        public SQSSourceOperator(
            string queueUrl,
            IAmazonSQS sqsClient,
            IDeserializer<TOutput> deserializer = null,
            ILogger<SQSSourceOperator<TOutput>> logger = null,
            bool deleteAfterProcess = true,
            int visibilityTimeout = 30,
            int maxNumberOfMessages = 10,
            int waitTimeSeconds = 20,
            Action<Exception, Message> errorHandler = null,
            int maxRetries = 3,
            int retryDelayMs = 1000)
        {
            _queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));
            _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
            _deserializer = deserializer ?? new DefaultJsonDeserializer<TOutput>();
            _logger = logger ?? NullLogger<SQSSourceOperator<TOutput>>.Instance;
            _deleteAfterProcess = deleteAfterProcess;
            _visibilityTimeout = visibilityTimeout;
            _maxNumberOfMessages = Math.Min(maxNumberOfMessages, 10);
            _waitTimeSeconds = Math.Min(waitTimeSeconds, 20);
            _errorHandler = errorHandler;
            _maxRetries = maxRetries;
            _retryDelay = TimeSpan.FromMilliseconds(retryDelayMs);
        }

        /// <summary>
        /// Starts the source operator and begins polling for messages.
        /// </summary>
        /// <param name="emit">The action to emit deserialized objects into the stream.</param>
        public void Start(Action<TOutput> emit)
        {
            if (emit == null) throw new ArgumentNullException(nameof(emit));
            if (_disposed) throw new ObjectDisposedException(nameof(SQSSourceOperator<TOutput>));

            _cancellationTokenSource = new CancellationTokenSource();
            _pollingTask = Task.Run(() => PollMessagesAsync(emit, _cancellationTokenSource.Token));
            _logger.LogInformation("SQS source operator started for queue {QueueUrl}", _queueUrl);
        }

        /// <summary>
        /// Stops the source operator and releases resources.
        /// </summary>
        public void Stop()
        {
            if (_disposed) return;

            _logger.LogInformation("Stopping SQS source operator for queue {QueueUrl}", _queueUrl);

            try
            {
                _cancellationTokenSource?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }

            try
            {
                _pollingTask?.Wait(TimeSpan.FromSeconds(30));
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error waiting for polling task to complete for queue {QueueUrl}", _queueUrl);
            }

            Dispose();
        }

        private async Task PollMessagesAsync(Action<TOutput> emit, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = _maxNumberOfMessages,
                    WaitTimeSeconds = _waitTimeSeconds,
                    VisibilityTimeout = _visibilityTimeout,
                    AttributeNames = new System.Collections.Generic.List<string> { "All" },
                    MessageAttributeNames = new System.Collections.Generic.List<string> { "All" }
                };

                try
                {
                    var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken);

                    foreach (var message in response.Messages)
                    {
                        try
                        {
                            var obj = _deserializer.Deserialize(message.Body);
                            emit(obj);

                            // Delete the message after successful processing if configured
                            if (_deleteAfterProcess)
                            {
                                await DeleteMessageWithRetryAsync(message.ReceiptHandle, cancellationToken);
                            }
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogError(ex, "Error processing SQS message {MessageId} from queue {QueueUrl}",
                                message.MessageId, _queueUrl);
                            _errorHandler?.Invoke(ex, message);

                            // Change visibility timeout to make message available for retry sooner
                            try
                            {
                                await _sqsClient.ChangeMessageVisibilityAsync(
                                    _queueUrl,
                                    message.ReceiptHandle,
                                    0, // Make immediately visible for retry
                                    cancellationToken);
                            }
                            catch (Exception visEx)
                            {
                                _logger.LogWarning(visEx, "Failed to change visibility for message {MessageId}", message.MessageId);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("SQS polling canceled for queue {QueueUrl}", _queueUrl);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error receiving messages from SQS queue {QueueUrl}", _queueUrl);
                    await Task.Delay(_retryDelay, cancellationToken);
                }
            }
        }

        private async Task DeleteMessageWithRetryAsync(string receiptHandle, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    await _sqsClient.DeleteMessageAsync(_queueUrl, receiptHandle, cancellationToken);
                    return;
                }
                catch (Exception ex) when (attempt < _maxRetries && ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to delete SQS message, attempt {Attempt} of {MaxRetries}",
                        attempt + 1, _maxRetries + 1);
                    await Task.Delay(_retryDelay * (attempt + 1), cancellationToken);
                }
            }
        }

        /// <summary>
        /// Disposes the SQS client.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            try
            {
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing cancellation token source for queue {QueueUrl}", _queueUrl);
            }

            try
            {
                _sqsClient?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing SQS client for queue {QueueUrl}", _queueUrl);
            }

            _logger.LogInformation("SQS source operator disposed for queue {QueueUrl}", _queueUrl);
        }
    }
}
