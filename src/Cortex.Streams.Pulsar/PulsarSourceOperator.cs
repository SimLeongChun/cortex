using Cortex.Streams.Operators;
using Cortex.Streams.Pulsar.Deserializers;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.Pulsar
{
    /// <summary>
    /// Pulsar source operator that consumes messages from a Pulsar topic.
    /// Supports authentication, proper resource management, and error handling.
    /// </summary>
    /// <typeparam name="TOutput">The type of objects to emit.</typeparam>
    public class PulsarSourceOperator<TOutput> : ISourceOperator<KeyValuePair<string, TOutput>>, IDisposable
    {
        private readonly string _serviceUrl;
        private readonly ConsumerOptions<ReadOnlySequence<byte>> _consumerOptions;
        private readonly string _topic;
        private readonly IDeserializer<TOutput> _deserializer;
        private readonly IPulsarClient _client;
        private readonly ILogger<PulsarSourceOperator<TOutput>> _logger;
        private readonly Action<Exception, IMessage<ReadOnlySequence<byte>>> _errorHandler;
        private readonly int _maxRetries;
        private readonly TimeSpan _retryDelay;
        private IConsumer<ReadOnlySequence<byte>> _consumer;
        private CancellationTokenSource _cts;
        private Task _consumeTask;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PulsarSourceOperator{TOutput}"/> class.
        /// </summary>
        /// <param name="serviceUrl">The Pulsar service URL.</param>
        /// <param name="topic">The topic to consume from.</param>
        /// <param name="deserializer">The deserializer to convert message bytes to TOutput objects.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="authenticationToken">Optional authentication token for secure connections.</param>
        /// <param name="errorHandler">Optional error handler for processing failures.</param>
        /// <param name="maxRetries">Maximum number of retry attempts for failed operations. Default is 3.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between retries. Default is 1000ms.</param>
        public PulsarSourceOperator(
            string serviceUrl,
            string topic,
            IDeserializer<TOutput> deserializer = null,
            ILogger<PulsarSourceOperator<TOutput>> logger = null,
            string authenticationToken = null,
            Action<Exception, IMessage<ReadOnlySequence<byte>>> errorHandler = null,
            int maxRetries = 3,
            int retryDelayMs = 1000)
        {
            _serviceUrl = serviceUrl ?? throw new ArgumentNullException(nameof(serviceUrl));
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _deserializer = deserializer ?? new DefaultJsonDeserializer<TOutput>();
            _logger = logger ?? NullLogger<PulsarSourceOperator<TOutput>>.Instance;
            _errorHandler = errorHandler;
            _maxRetries = maxRetries;
            _retryDelay = TimeSpan.FromMilliseconds(retryDelayMs);
            _consumerOptions = null;

            var clientBuilder = PulsarClient.Builder()
                .ServiceUrl(new Uri(_serviceUrl));

            // Add authentication if provided
            if (!string.IsNullOrEmpty(authenticationToken))
            {
                clientBuilder.Authentication(AuthenticationFactory.Token(authenticationToken));
            }

            _client = clientBuilder.Build();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PulsarSourceOperator{TOutput}"/> class with custom consumer options.
        /// </summary>
        /// <param name="serviceUrl">The Pulsar service URL.</param>
        /// <param name="consumerOptions">Custom consumer options.</param>
        /// <param name="deserializer">The deserializer to convert message bytes to TOutput objects.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="authenticationToken">Optional authentication token for secure connections.</param>
        /// <param name="errorHandler">Optional error handler for processing failures.</param>
        /// <param name="maxRetries">Maximum number of retry attempts for failed operations. Default is 3.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between retries. Default is 1000ms.</param>
        public PulsarSourceOperator(
            string serviceUrl,
            ConsumerOptions<ReadOnlySequence<byte>> consumerOptions,
            IDeserializer<TOutput> deserializer = null,
            ILogger<PulsarSourceOperator<TOutput>> logger = null,
            string authenticationToken = null,
            Action<Exception, IMessage<ReadOnlySequence<byte>>> errorHandler = null,
            int maxRetries = 3,
            int retryDelayMs = 1000)
        {
            _serviceUrl = serviceUrl ?? throw new ArgumentNullException(nameof(serviceUrl));
            _consumerOptions = consumerOptions ?? throw new ArgumentNullException(nameof(consumerOptions));
            _deserializer = deserializer ?? new DefaultJsonDeserializer<TOutput>();
            _logger = logger ?? NullLogger<PulsarSourceOperator<TOutput>>.Instance;
            _errorHandler = errorHandler;
            _maxRetries = maxRetries;
            _retryDelay = TimeSpan.FromMilliseconds(retryDelayMs);

            var clientBuilder = PulsarClient.Builder()
                .ServiceUrl(new Uri(_serviceUrl));

            // Add authentication if provided
            if (!string.IsNullOrEmpty(authenticationToken))
            {
                clientBuilder.Authentication(AuthenticationFactory.Token(authenticationToken));
            }

            _client = clientBuilder.Build();
        }

        /// <summary>
        /// Starts the source operator and begins consuming messages.
        /// </summary>
        /// <param name="emit">The action to emit deserialized key-value pairs into the stream.</param>
        public void Start(Action<KeyValuePair<string, TOutput>> emit)
        {
            if (emit == null) throw new ArgumentNullException(nameof(emit));
            if (_disposed) throw new ObjectDisposedException(nameof(PulsarSourceOperator<TOutput>));

            _cts = new CancellationTokenSource();

            _consumer = _consumerOptions == null
                ? _client.NewConsumer()
                    .Topic(_topic)
                    .InitialPosition(SubscriptionInitialPosition.Earliest)
                    .SubscriptionName($"cortex-subscription-{_topic}-{Environment.MachineName}")
                    .Create()
                : _client.CreateConsumer(_consumerOptions);

            _logger.LogInformation("Pulsar source operator started for topic {Topic}", _topic ?? "custom");

            _consumeTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var message in _consumer.Messages(_cts.Token))
                    {
                        try
                        {
                            var key = message.Key ?? string.Empty;
                            var output = _deserializer.Deserialize(message.Data);
                            emit(new KeyValuePair<string, TOutput>(key, output));

                            // Acknowledge with retry
                            await AcknowledgeWithRetryAsync(message, _cts.Token);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogError(ex, "Error processing Pulsar message from topic {Topic}", _topic);
                            _errorHandler?.Invoke(ex, message);

                            // Negative acknowledge to trigger redelivery
                            try
                            {
                                await _consumer.RedeliverUnacknowledgedMessages(_cts.Token);
                            }
                            catch (Exception nackEx)
                            {
                                _logger.LogWarning(nackEx, "Failed to negative acknowledge message from topic {Topic}", _topic);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Pulsar consume loop canceled for topic {Topic}", _topic);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in Pulsar consume loop for topic {Topic}", _topic);
                }
                finally
                {
                    await DisposeResourcesAsync();
                }
            }, _cts.Token);
        }

        private async Task AcknowledgeWithRetryAsync(IMessage<ReadOnlySequence<byte>> message, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    await _consumer.Acknowledge(message, cancellationToken);
                    return;
                }
                catch (Exception ex) when (attempt < _maxRetries && ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to acknowledge Pulsar message, attempt {Attempt} of {MaxRetries}", attempt + 1, _maxRetries);
                    await Task.Delay(_retryDelay * (attempt + 1), cancellationToken);
                }
            }
        }

        /// <summary>
        /// Stops the source operator and releases resources.
        /// </summary>
        public void Stop()
        {
            if (_disposed)
                return;

            _logger.LogInformation("Stopping Pulsar source operator for topic {Topic}", _topic);

            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }

            try
            {
                _consumeTask?.ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error waiting for consume task to complete for topic {Topic}", _topic);
            }

            Dispose();
        }

        private async Task DisposeResourcesAsync()
        {
            try
            {
                if (_consumer != null)
                {
                    await _consumer.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Pulsar consumer for topic {Topic}", _topic);
            }

            try
            {
                if (_client != null)
                {
                    await _client.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Pulsar client for topic {Topic}", _topic);
            }
        }

        /// <summary>
        /// Disposes the Pulsar consumer and client.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _cts?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing cancellation token source for topic {Topic}", _topic);
            }

            _logger.LogInformation("Pulsar source operator disposed for topic {Topic}", _topic);
        }
    }
}
