using Azure.Messaging.ServiceBus;
using Cortex.Streams.AzureServiceBus.Deserializers;
using Cortex.Streams.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.AzureServiceBus
{
    /// <summary>
    /// Azure Service Bus Source Operator with deserialization support, dead-letter handling, and proper resource management.
    /// </summary>
    /// <typeparam name="TOutput">The type of objects to emit.</typeparam>
    public class AzureServiceBusSourceOperator<TOutput> : ISourceOperator<TOutput>, IDisposable
    {
        private readonly string _connectionString;
        private readonly string _queueOrTopicName;
        private readonly string _subscriptionName;
        private readonly IDeserializer<TOutput> _deserializer;
        private readonly ServiceBusProcessorOptions _serviceBusProcessorOptions;
        private readonly ILogger<AzureServiceBusSourceOperator<TOutput>> _logger;
        private readonly Action<Exception, ProcessMessageEventArgs> _errorHandler;
        private readonly Func<TOutput, Exception, Task<bool>> _deadLetterHandler;
        private readonly int _maxDeliveryCount;
        private readonly int _maxRetries;
        private readonly TimeSpan _retryDelay;
        private ServiceBusClient _client;
        private ServiceBusProcessor _processor;
        private Action<TOutput> _emitAction;
        private bool _isRunning;
        private bool _disposed;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureServiceBusSourceOperator{TOutput}"/> class for a queue.
        /// </summary>
        /// <param name="connectionString">The Azure Service Bus connection string.</param>
        /// <param name="queueOrTopicName">The name of the queue or topic to consume from.</param>
        /// <param name="deserializer">The deserializer to convert message strings to TOutput objects.</param>
        /// <param name="serviceBusProcessorOptions">Optional processor options.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="errorHandler">Optional error handler for processing failures.</param>
        /// <param name="deadLetterHandler">Optional dead-letter handler. Returns true to dead-letter the message, false to abandon.</param>
        /// <param name="maxDeliveryCount">Maximum delivery attempts before dead-lettering. Default is 10.</param>
        /// <param name="maxRetries">Maximum number of retry attempts for acknowledgment. Default is 3.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between retries. Default is 100ms.</param>
        public AzureServiceBusSourceOperator(
            string connectionString,
            string queueOrTopicName,
            IDeserializer<TOutput> deserializer = null,
            ServiceBusProcessorOptions serviceBusProcessorOptions = null,
            ILogger<AzureServiceBusSourceOperator<TOutput>> logger = null,
            Action<Exception, ProcessMessageEventArgs> errorHandler = null,
            Func<TOutput, Exception, Task<bool>> deadLetterHandler = null,
            int maxDeliveryCount = 10,
            int maxRetries = 3,
            int retryDelayMs = 100)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _queueOrTopicName = queueOrTopicName ?? throw new ArgumentNullException(nameof(queueOrTopicName));
            _subscriptionName = null;

            _deserializer = deserializer ?? new DefaultJsonDeserializer<TOutput>();
            _logger = logger ?? NullLogger<AzureServiceBusSourceOperator<TOutput>>.Instance;
            _errorHandler = errorHandler;
            _deadLetterHandler = deadLetterHandler;
            _maxDeliveryCount = maxDeliveryCount;
            _maxRetries = maxRetries;
            _retryDelay = TimeSpan.FromMilliseconds(retryDelayMs);

            _serviceBusProcessorOptions = serviceBusProcessorOptions ?? new ServiceBusProcessorOptions()
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1,
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5),
                PrefetchCount = 10
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureServiceBusSourceOperator{TOutput}"/> class for a topic subscription.
        /// </summary>
        /// <param name="connectionString">The Azure Service Bus connection string.</param>
        /// <param name="topicName">The name of the topic to consume from.</param>
        /// <param name="subscriptionName">The name of the subscription.</param>
        /// <param name="deserializer">The deserializer to convert message strings to TOutput objects.</param>
        /// <param name="serviceBusProcessorOptions">Optional processor options.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="errorHandler">Optional error handler for processing failures.</param>
        /// <param name="deadLetterHandler">Optional dead-letter handler. Returns true to dead-letter the message, false to abandon.</param>
        /// <param name="maxDeliveryCount">Maximum delivery attempts before dead-lettering. Default is 10.</param>
        /// <param name="maxRetries">Maximum number of retry attempts for acknowledgment. Default is 3.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between retries. Default is 100ms.</param>
        public AzureServiceBusSourceOperator(
            string connectionString,
            string topicName,
            string subscriptionName,
            IDeserializer<TOutput> deserializer = null,
            ServiceBusProcessorOptions serviceBusProcessorOptions = null,
            ILogger<AzureServiceBusSourceOperator<TOutput>> logger = null,
            Action<Exception, ProcessMessageEventArgs> errorHandler = null,
            Func<TOutput, Exception, Task<bool>> deadLetterHandler = null,
            int maxDeliveryCount = 10,
            int maxRetries = 3,
            int retryDelayMs = 100)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _queueOrTopicName = topicName ?? throw new ArgumentNullException(nameof(topicName));
            _subscriptionName = subscriptionName ?? throw new ArgumentNullException(nameof(subscriptionName));

            _deserializer = deserializer ?? new DefaultJsonDeserializer<TOutput>();
            _logger = logger ?? NullLogger<AzureServiceBusSourceOperator<TOutput>>.Instance;
            _errorHandler = errorHandler;
            _deadLetterHandler = deadLetterHandler;
            _maxDeliveryCount = maxDeliveryCount;
            _maxRetries = maxRetries;
            _retryDelay = TimeSpan.FromMilliseconds(retryDelayMs);

            _serviceBusProcessorOptions = serviceBusProcessorOptions ?? new ServiceBusProcessorOptions()
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1,
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5),
                PrefetchCount = 10
            };
        }

        /// <summary>
        /// Starts the source operator.
        /// </summary>
        /// <param name="emit">The action to emit deserialized objects into the stream.</param>
        public void Start(Action<TOutput> emit)
        {
            if (emit == null) throw new ArgumentNullException(nameof(emit));
            if (_disposed) throw new ObjectDisposedException(nameof(AzureServiceBusSourceOperator<TOutput>));

            lock (_lock)
            {
                if (_isRunning) throw new InvalidOperationException("AzureServiceBusSourceOperator is already running.");

                _emitAction = emit;

                var clientOptions = new ServiceBusClientOptions
                {
                    TransportType = ServiceBusTransportType.AmqpTcp,
                    RetryOptions = new ServiceBusRetryOptions
                    {
                        Mode = ServiceBusRetryMode.Exponential,
                        MaxRetries = _maxRetries,
                        Delay = _retryDelay,
                        MaxDelay = TimeSpan.FromSeconds(30)
                    }
                };

                _client = new ServiceBusClient(_connectionString, clientOptions);

                // Create processor for queue or topic subscription
                _processor = string.IsNullOrEmpty(_subscriptionName)
                    ? _client.CreateProcessor(_queueOrTopicName, _serviceBusProcessorOptions)
                    : _client.CreateProcessor(_queueOrTopicName, _subscriptionName, _serviceBusProcessorOptions);

                _processor.ProcessMessageAsync += MessageHandler;
                _processor.ProcessErrorAsync += ErrorHandler;

                Task.Run(async () =>
                {
                    try
                    {
                        await _processor.StartProcessingAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to start Azure Service Bus processor for {QueueOrTopicName}", _queueOrTopicName);
                    }
                });

                _isRunning = true;
                _logger.LogInformation("Azure Service Bus source operator started for {QueueOrTopicName}", _queueOrTopicName);
            }
        }

        /// <summary>
        /// Stops the source operator.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _disposed) return;

            lock (_lock)
            {
                _logger.LogInformation("Stopping Azure Service Bus source operator for {QueueOrTopicName}", _queueOrTopicName);

                try
                {
                    _processor?.StopProcessingAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping Azure Service Bus processor for {QueueOrTopicName}", _queueOrTopicName);
                }

                _isRunning = false;
            }

            Dispose();
            _logger.LogInformation("Azure Service Bus source operator stopped for {QueueOrTopicName}", _queueOrTopicName);
        }

        /// <summary>
        /// Handles incoming messages.
        /// </summary>
        private async Task MessageHandler(ProcessMessageEventArgs args)
        {
            TOutput obj = default;
            try
            {
                var body = args.Message.Body.ToString();
                obj = _deserializer.Deserialize(body);
                _emitAction?.Invoke(obj);

                // Complete the message with retry
                await CompleteMessageWithRetryAsync(args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId} from Azure Service Bus {QueueOrTopicName}",
                    args.Message.MessageId, _queueOrTopicName);

                _errorHandler?.Invoke(ex, args);

                // Check if we should dead-letter the message
                var shouldDeadLetter = args.Message.DeliveryCount >= _maxDeliveryCount;

                if (_deadLetterHandler != null && obj != null)
                {
                    try
                    {
                        shouldDeadLetter = await _deadLetterHandler(obj, ex);
                    }
                    catch (Exception dlEx)
                    {
                        _logger.LogWarning(dlEx, "Dead-letter handler threw exception for message {MessageId}", args.Message.MessageId);
                    }
                }

                if (shouldDeadLetter)
                {
                    try
                    {
                        await args.DeadLetterMessageAsync(args.Message, "MaxDeliveryExceeded",
                            $"Message exceeded max delivery count ({_maxDeliveryCount}). Error: {ex.Message}");
                        _logger.LogWarning("Message {MessageId} dead-lettered after {DeliveryCount} attempts",
                            args.Message.MessageId, args.Message.DeliveryCount);
                    }
                    catch (Exception dlEx)
                    {
                        _logger.LogError(dlEx, "Failed to dead-letter message {MessageId}", args.Message.MessageId);
                    }
                }
                else
                {
                    try
                    {
                        await args.AbandonMessageAsync(args.Message);
                    }
                    catch (Exception abandonEx)
                    {
                        _logger.LogError(abandonEx, "Failed to abandon message {MessageId}", args.Message.MessageId);
                    }
                }
            }
        }

        private async Task CompleteMessageWithRetryAsync(ProcessMessageEventArgs args)
        {
            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    await args.CompleteMessageAsync(args.Message);
                    return;
                }
                catch (Exception ex) when (attempt < _maxRetries)
                {
                    _logger.LogWarning(ex, "Failed to complete message {MessageId}, attempt {Attempt} of {MaxRetries}",
                        args.Message.MessageId, attempt + 1, _maxRetries + 1);
                    await Task.Delay(_retryDelay * (attempt + 1));
                }
            }
        }

        /// <summary>
        /// Handles errors during message processing.
        /// </summary>
        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Error in Azure Service Bus source operator for {QueueOrTopicName}. Source: {ErrorSource}, Namespace: {FullyQualifiedNamespace}, EntityPath: {EntityPath}",
                _queueOrTopicName,
                args.ErrorSource,
                args.FullyQualifiedNamespace,
                args.EntityPath);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes the ServiceBusProcessor and ServiceBusClient.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            lock (_lock)
            {
                _disposed = true;

                try
                {
                    _processor?.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing Azure Service Bus processor for {QueueOrTopicName}", _queueOrTopicName);
                }

                try
                {
                    _client?.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing Azure Service Bus client for {QueueOrTopicName}", _queueOrTopicName);
                }
            }
        }
    }
}
