using Azure.Messaging.ServiceBus;
using Cortex.Streams.AzureServiceBus.Serializers;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.AzureServiceBus
{
    /// <summary>
    /// Azure Service Bus Sink Operator with serialization support and proper resource management.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of objects to send.</typeparam>
    public class AzureServiceBusSinkOperator<TInput> : ISinkOperator<TInput>, IErrorHandlingEnabled, IDisposable
    {
        private static readonly string OperatorName = $"AzureServiceBusSinkOperator<{typeof(TInput).Name}>";

        private readonly string _connectionString;
        private readonly string _queueOrTopicName;
        private readonly ISerializer<TInput> _serializer;
        private readonly ILogger<AzureServiceBusSinkOperator<TInput>> _logger;
        private readonly Func<TInput, string> _sessionIdSelector;
        private readonly Func<TInput, string> _partitionKeySelector;
        private readonly Func<TInput, TimeSpan?> _timeToLiveSelector;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;
        private ServiceBusClient _client;
        private ServiceBusSender _sender;
        private bool _isRunning;
        private bool _disposed;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureServiceBusSinkOperator{TInput}"/> class.
        /// </summary>
        /// <param name="connectionString">The Azure Service Bus connection string.</param>
        /// <param name="queueOrTopicName">The name of the queue or topic to send messages to.</param>
        /// <param name="serializer">The serializer to convert TInput objects to strings.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="sessionIdSelector">Optional selector for session ID (for session-enabled queues/topics).</param>
        /// <param name="partitionKeySelector">Optional selector for partition key.</param>
        /// <param name="timeToLiveSelector">Optional selector for message time-to-live.</param>
        public AzureServiceBusSinkOperator(
            string connectionString,
            string queueOrTopicName,
            ISerializer<TInput> serializer = null,
            ILogger<AzureServiceBusSinkOperator<TInput>> logger = null,
            Func<TInput, string> sessionIdSelector = null,
            Func<TInput, string> partitionKeySelector = null,
            Func<TInput, TimeSpan?> timeToLiveSelector = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _queueOrTopicName = queueOrTopicName ?? throw new ArgumentNullException(nameof(queueOrTopicName));

            _serializer = serializer ?? new DefaultJsonSerializer<TInput>();
            _logger = logger ?? NullLogger<AzureServiceBusSinkOperator<TInput>>.Instance;
            _sessionIdSelector = sessionIdSelector;
            _partitionKeySelector = partitionKeySelector;
            _timeToLiveSelector = timeToLiveSelector;
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
        /// Starts the sink operator.
        /// </summary>
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AzureServiceBusSinkOperator<TInput>));

            lock (_lock)
            {
                if (_isRunning) return;

                var clientOptions = new ServiceBusClientOptions
                {
                    TransportType = ServiceBusTransportType.AmqpTcp,
                    RetryOptions = new ServiceBusRetryOptions
                    {
                        Mode = ServiceBusRetryMode.Exponential,
                        MaxRetries = 3,
                        Delay = TimeSpan.FromMilliseconds(100),
                        MaxDelay = TimeSpan.FromSeconds(30)
                    }
                };

                _client = new ServiceBusClient(_connectionString, clientOptions);
                _sender = _client.CreateSender(_queueOrTopicName);

                _isRunning = true;
                _logger.LogInformation("Azure Service Bus sink operator started for {QueueOrTopicName}", _queueOrTopicName);
            }
        }

        /// <summary>
        /// Processes the input object by serializing it and sending it to Azure Service Bus.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The input object to send.</param>
        public void Process(TInput input)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AzureServiceBusSinkOperator<TInput>));
            if (!_isRunning)
            {
                _logger.LogWarning("AzureServiceBusSinkOperator is not running. Call Start() before processing messages.");
                return;
            }

            if (input == null)
            {
                _logger.LogDebug("AzureServiceBusSinkOperator received null input. Skipping.");
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
            var message = CreateServiceBusMessage(input);
            _sender.SendMessageAsync(message).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sends a batch of messages to Azure Service Bus.
        /// </summary>
        /// <param name="inputs">The batch of input objects to send.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the async operation.</returns>
        public async Task ProcessBatchAsync(IEnumerable<TInput> inputs, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AzureServiceBusSinkOperator<TInput>));
            if (!_isRunning)
            {
                _logger.LogWarning("AzureServiceBusSinkOperator is not running. Call Start() before processing messages.");
                return;
            }

            if (inputs == null) return;

            using var messageBatch = await _sender.CreateMessageBatchAsync(cancellationToken);
            var failedMessages = new List<TInput>();

            foreach (var input in inputs)
            {
                if (input == null) continue;

                var message = CreateServiceBusMessage(input);
                if (!messageBatch.TryAddMessage(message))
                {
                    // Batch is full, send it and start a new one
                    await _sender.SendMessagesAsync(messageBatch, cancellationToken);
                    failedMessages.Add(input);
                }
            }

            // Send remaining messages
            if (messageBatch.Count > 0)
            {
                await _sender.SendMessagesAsync(messageBatch, cancellationToken);
            }

            // Process messages that didn't fit in the batch individually
            foreach (var failedInput in failedMessages)
            {
                var message = CreateServiceBusMessage(failedInput);
                await _sender.SendMessageAsync(message, cancellationToken);
            }
        }

        private ServiceBusMessage CreateServiceBusMessage(TInput input)
        {
            var serializedMessage = _serializer.Serialize(input);
            var serviceBusMessage = new ServiceBusMessage(serializedMessage)
            {
                ContentType = "application/json",
                Subject = typeof(TInput).Name
            };

            // Set session ID if selector is provided
            if (_sessionIdSelector != null)
            {
                serviceBusMessage.SessionId = _sessionIdSelector(input);
            }

            // Set partition key if selector is provided
            if (_partitionKeySelector != null)
            {
                serviceBusMessage.PartitionKey = _partitionKeySelector(input);
            }

            // Set time-to-live if selector is provided
            if (_timeToLiveSelector != null)
            {
                var ttl = _timeToLiveSelector(input);
                if (ttl.HasValue)
                {
                    serviceBusMessage.TimeToLive = ttl.Value;
                }
            }

            return serviceBusMessage;
        }

        /// <summary>
        /// Stops the sink operator.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _disposed) return;

            lock (_lock)
            {
                _logger.LogInformation("Stopping Azure Service Bus sink operator for {QueueOrTopicName}", _queueOrTopicName);
                _isRunning = false;
            }

            Dispose();
            _logger.LogInformation("Azure Service Bus sink operator stopped for {QueueOrTopicName}", _queueOrTopicName);
        }

        /// <summary>
        /// Disposes the Azure Service Bus client and sender.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            lock (_lock)
            {
                _disposed = true;

                try
                {
                    _sender?.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing Azure Service Bus sender for {QueueOrTopicName}", _queueOrTopicName);
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
