using Confluent.Kafka;
using Cortex.Streams.Kafka.Deserializers;
using Cortex.Streams.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.Kafka
{
    /// <summary>
    /// Kafka source operator that consumes messages from a Kafka topic.
    /// Supports manual/auto commit, security configuration, and proper resource management.
    /// </summary>
    /// <typeparam name="TOutput">The type of objects to emit.</typeparam>
    public sealed class KafkaSourceOperator<TOutput> : ISourceOperator<TOutput>, IDisposable
    {
        private readonly string _bootstrapServers;
        private readonly string _topic;
        private readonly IConsumer<Ignore, TOutput> _consumer;
        private readonly ILogger<KafkaSourceOperator<TOutput>> _logger;
        private readonly bool _enableAutoCommit;
        private CancellationTokenSource _cts;
        private Task _consumeTask;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="KafkaSourceOperator{TOutput}"/> class.
        /// </summary>
        /// <param name="bootstrapServers">The Kafka bootstrap servers.</param>
        /// <param name="topic">The topic to consume from.</param>
        /// <param name="groupId">The consumer group ID. If null, generates a unique ID based on the topic name.</param>
        /// <param name="config">Optional consumer configuration. If provided, overrides other settings.</param>
        /// <param name="deserializer">The deserializer to convert message bytes to TOutput objects.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="enableAutoCommit">Whether to enable automatic offset commits. Default is false for production reliability.</param>
        public KafkaSourceOperator(
            string bootstrapServers,
            string topic,
            string groupId = null,
            ConsumerConfig config = null,
            IDeserializer<TOutput> deserializer = null,
            ILogger<KafkaSourceOperator<TOutput>> logger = null,
            bool enableAutoCommit = false)
        {
            _bootstrapServers = bootstrapServers ?? throw new ArgumentNullException(nameof(bootstrapServers));
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _logger = logger ?? NullLogger<KafkaSourceOperator<TOutput>>.Instance;
            _enableAutoCommit = enableAutoCommit;

            var consumerConfig = config ?? new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = groupId ?? $"cortex-consumer-{topic}-{Environment.MachineName}",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = _enableAutoCommit,
                EnableAutoOffsetStore = _enableAutoCommit,
                // Connection settings for reliability
                SessionTimeoutMs = 30000,
                HeartbeatIntervalMs = 10000,
                MaxPollIntervalMs = 300000,
            };

            deserializer ??= new DefaultJsonDeserializer<TOutput>();

            _consumer = new ConsumerBuilder<Ignore, TOutput>(consumerConfig)
                .SetValueDeserializer(deserializer)
                .SetErrorHandler((_, e) => _logger.LogError("Kafka consumer error: {Reason}", e.Reason))
                .SetPartitionsAssignedHandler((c, partitions) =>
                    _logger.LogInformation("Partitions assigned: {Partitions}", string.Join(", ", partitions)))
                .SetPartitionsRevokedHandler((c, partitions) =>
                    _logger.LogInformation("Partitions revoked: {Partitions}", string.Join(", ", partitions)))
                .Build();
        }

        /// <summary>
        /// Starts the source operator and begins consuming messages.
        /// </summary>
        /// <param name="emit">The action to emit deserialized objects into the stream.</param>
        public void Start(Action<TOutput> emit)
        {
            if (emit == null) throw new ArgumentNullException(nameof(emit));
            if (_disposed) throw new ObjectDisposedException(nameof(KafkaSourceOperator<TOutput>));

            _cts = new CancellationTokenSource();
            _consumer.Subscribe(_topic);
            _logger.LogInformation("Kafka source operator started for topic {Topic}", _topic);

            _consumeTask = Task.Run(() =>
            {
                try
                {
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var result = _consumer.Consume(_cts.Token);
                            if (result?.Message != null)
                            {
                                emit(result.Message.Value);

                                // Manual commit if auto-commit is disabled
                                if (!_enableAutoCommit)
                                {
                                    try
                                    {
                                        _consumer.Commit(result);
                                    }
                                    catch (KafkaException ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to commit offset for topic {Topic}", _topic);
                                    }
                                }
                            }
                        }
                        catch (ConsumeException ex)
                        {
                            _logger.LogError(ex, "Error consuming message from topic {Topic}: {Reason}", _topic, ex.Error.Reason);
                            // Transport-level errors are logged; the message will be redelivered by Kafka
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Kafka consume loop canceled for topic {Topic}", _topic);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in Kafka consume loop for topic {Topic}", _topic);
                }
                finally
                {
                    try
                    {
                        _consumer.Close();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error closing Kafka consumer for topic {Topic}", _topic);
                    }
                }
            }, _cts.Token);
        }

        /// <summary>
        /// Stops the source operator and releases resources.
        /// </summary>
        public void Stop()
        {
            if (_cts == null || _disposed)
                return;

            _logger.LogInformation("Stopping Kafka source operator for topic {Topic}", _topic);

            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }

            try
            {
                _consumeTask?.Wait(TimeSpan.FromSeconds(30));
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error waiting for consume task to complete for topic {Topic}", _topic);
            }

            Dispose();
        }

        /// <summary>
        /// Disposes the Kafka consumer and cancellation token.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _consumer?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Kafka consumer for topic {Topic}", _topic);
            }

            try
            {
                _cts?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing cancellation token source for topic {Topic}", _topic);
            }

            _logger.LogInformation("Kafka source operator disposed for topic {Topic}", _topic);
        }
    }
}
