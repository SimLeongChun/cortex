using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Cortex.Streams.AWSSQS.Serializers;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.AWSSQS
{
    /// <summary>
    /// AWS SQS Sink Operator with serialization support, credential injection, and FIFO queue support.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of objects to send.</typeparam>
    public class SQSSinkOperator<TInput> : ISinkOperator<TInput>, IErrorHandlingEnabled, IDisposable
    {
        private static readonly string OperatorName = $"SQSSinkOperator<{typeof(TInput).Name}>";

        private readonly string _queueUrl;
        private readonly IAmazonSQS _sqsClient;
        private readonly ISerializer<TInput> _serializer;
        private readonly ILogger<SQSSinkOperator<TInput>> _logger;
        private readonly Func<TInput, string> _messageGroupIdSelector;
        private readonly Func<TInput, string> _deduplicationIdSelector;
        private readonly int _delaySeconds;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;
        private bool _isRunning;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SQSSinkOperator{TInput}"/> class.
        /// </summary>
        /// <param name="queueUrl">The SQS queue URL.</param>
        /// <param name="region">The AWS region. Default is us-east-1.</param>
        /// <param name="credentials">Optional AWS credentials. If null, uses default credential chain.</param>
        /// <param name="serializer">The serializer to convert TInput objects to strings.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="messageGroupIdSelector">Optional selector for FIFO queue message group ID.</param>
        /// <param name="deduplicationIdSelector">Optional selector for FIFO queue deduplication ID.</param>
        /// <param name="delaySeconds">Delay in seconds before messages become available. Default is 0.</param>
        public SQSSinkOperator(
            string queueUrl,
            RegionEndpoint region = null,
            AWSCredentials credentials = null,
            ISerializer<TInput> serializer = null,
            ILogger<SQSSinkOperator<TInput>> logger = null,
            Func<TInput, string> messageGroupIdSelector = null,
            Func<TInput, string> deduplicationIdSelector = null,
            int delaySeconds = 0)
        {
            _queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));
            _serializer = serializer ?? new DefaultJsonSerializer<TInput>();
            _logger = logger ?? NullLogger<SQSSinkOperator<TInput>>.Instance;
            _messageGroupIdSelector = messageGroupIdSelector;
            _deduplicationIdSelector = deduplicationIdSelector;
            _delaySeconds = delaySeconds;

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
        /// Initializes a new instance of the <see cref="SQSSinkOperator{TInput}"/> class with an existing SQS client.
        /// </summary>
        /// <param name="queueUrl">The SQS queue URL.</param>
        /// <param name="sqsClient">An existing IAmazonSQS client instance.</param>
        /// <param name="serializer">The serializer to convert TInput objects to strings.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="messageGroupIdSelector">Optional selector for FIFO queue message group ID.</param>
        /// <param name="deduplicationIdSelector">Optional selector for FIFO queue deduplication ID.</param>
        /// <param name="delaySeconds">Delay in seconds before messages become available. Default is 0.</param>
        public SQSSinkOperator(
            string queueUrl,
            IAmazonSQS sqsClient,
            ISerializer<TInput> serializer = null,
            ILogger<SQSSinkOperator<TInput>> logger = null,
            Func<TInput, string> messageGroupIdSelector = null,
            Func<TInput, string> deduplicationIdSelector = null,
            int delaySeconds = 0)
        {
            _queueUrl = queueUrl ?? throw new ArgumentNullException(nameof(queueUrl));
            _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
            _serializer = serializer ?? new DefaultJsonSerializer<TInput>();
            _logger = logger ?? NullLogger<SQSSinkOperator<TInput>>.Instance;
            _messageGroupIdSelector = messageGroupIdSelector;
            _deduplicationIdSelector = deduplicationIdSelector;
            _delaySeconds = delaySeconds;
        }

        /// <summary>
        /// Sets the stream-level error handling options.
        /// Called by the Stream when the pipeline is built.
        /// </summary>
        /// <param name="options">The stream execution options containing error handling configuration.</param>
        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;
        }

        /// <summary>
        /// Processes the input object by serializing it and sending it to SQS.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The input object to send.</param>
        public void Process(TInput input)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SQSSinkOperator<TInput>));
            if (!_isRunning)
            {
                _logger.LogWarning("SQSSinkOperator is not running. Call Start() before processing messages.");
                return;
            }

            if (input == null)
            {
                _logger.LogDebug("SQSSinkOperator received null input. Skipping.");
                return;
            }

            // Use core error handling for message processing
            ErrorHandlingHelper.TryExecute(
                _executionOptions,
                OperatorName,
                input,
                (Action<TInput>)SendMessage);
        }

        private void SendMessage(TInput input)
        {
            var serializedMessage = _serializer.Serialize(input);
            var request = new SendMessageRequest
            {
                QueueUrl = _queueUrl,
                MessageBody = serializedMessage,
                DelaySeconds = _delaySeconds
            };

            // Add FIFO queue attributes if selectors are provided
            if (_messageGroupIdSelector != null)
            {
                request.MessageGroupId = _messageGroupIdSelector(input);
            }

            if (_deduplicationIdSelector != null)
            {
                request.MessageDeduplicationId = _deduplicationIdSelector(input);
            }
            else if (_queueUrl.EndsWith(".fifo", StringComparison.OrdinalIgnoreCase))
            {
                // Auto-generate deduplication ID for FIFO queues if not provided
                request.MessageDeduplicationId = Guid.NewGuid().ToString();
            }

            var response = _sqsClient.SendMessageAsync(request).GetAwaiter().GetResult();
            _logger.LogDebug("Message sent to SQS queue {QueueUrl}, MessageId: {MessageId}", _queueUrl, response.MessageId);
        }

        /// <summary>
        /// Starts the sink operator.
        /// </summary>
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SQSSinkOperator<TInput>));
            if (_isRunning) return;

            _isRunning = true;
            _logger.LogInformation("SQS sink operator started for queue {QueueUrl}", _queueUrl);
        }

        /// <summary>
        /// Stops the sink operator and releases resources.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _disposed) return;

            _logger.LogInformation("Stopping SQS sink operator for queue {QueueUrl}", _queueUrl);
            _isRunning = false;

            Dispose();
            _logger.LogInformation("SQS sink operator stopped for queue {QueueUrl}", _queueUrl);
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
                _sqsClient?.Dispose();
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
        }
    }
}
