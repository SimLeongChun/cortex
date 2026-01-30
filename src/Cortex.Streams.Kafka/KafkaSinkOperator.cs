using Confluent.Kafka;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Kafka.Serializers;
using Cortex.Streams.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.Kafka
{
    /// <summary>
    /// Kafka sink operator that produces messages to a Kafka topic.
    /// Supports retry policies, error handling, and proper resource management.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of objects to send.</typeparam>
    public sealed class KafkaSinkOperator<TInput> : ISinkOperator<TInput>, IErrorHandlingEnabled, IDisposable
    {
        private static readonly string OperatorName = $"KafkaSinkOperator<{typeof(TInput).Name}>";

        private readonly string _bootstrapServers;
        private readonly string _topic;
        private readonly IProducer<Null, TInput> _producer;
        private readonly ILogger<KafkaSinkOperator<TInput>> _logger;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;
        private bool _disposed;
        private bool _isRunning;

        /// <summary>
        /// Initializes a new instance of the <see cref="KafkaSinkOperator{TInput}"/> class.
        /// </summary>
        /// <param name="bootstrapServers">The Kafka bootstrap servers.</param>
        /// <param name="topic">The topic to produce to.</param>
        /// <param name="config">Optional producer configuration.</param>
        /// <param name="serializer">The serializer to convert TInput objects to bytes.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        public KafkaSinkOperator(
            string bootstrapServers,
            string topic,
            ProducerConfig config = null,
            ISerializer<TInput> serializer = null,
            ILogger<KafkaSinkOperator<TInput>> logger = null)
        {
            _bootstrapServers = bootstrapServers ?? throw new ArgumentNullException(nameof(bootstrapServers));
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _logger = logger ?? NullLogger<KafkaSinkOperator<TInput>>.Instance;

            var producerConfig = config ?? new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                // Reliability settings
                Acks = Acks.All,
                EnableIdempotence = true,
                MaxInFlight = 5,
                MessageSendMaxRetries = 3,
                RetryBackoffMs = 100,
                // Batching for performance
                LingerMs = 5,
                BatchSize = 16384,
            };

            serializer ??= new DefaultJsonSerializer<TInput>();

            _producer = new ProducerBuilder<Null, TInput>(producerConfig)
                .SetValueSerializer(serializer)
                .SetErrorHandler((_, e) => _logger.LogError("Kafka producer error: {Reason}", e.Reason))
                .Build();
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
        /// Processes the input object by serializing it and sending it to the Kafka topic.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The input object to send.</param>
        public void Process(TInput input)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KafkaSinkOperator<TInput>));
            if (!_isRunning)
            {
                _logger.LogWarning("KafkaSinkOperator is not running. Call Start() before processing messages.");
                return;
            }

            if (input == null)
            {
                _logger.LogDebug("KafkaSinkOperator received null input. Skipping.");
                return;
            }

            // Use core error handling for message processing
            ErrorHandlingHelper.TryExecute(
                _executionOptions,
                OperatorName,
                input,
                (Action<TInput>)ProduceMessage);
        }

        private void ProduceMessage(TInput input)
        {
            _producer.Produce(_topic, new Message<Null, TInput> { Value = input }, deliveryReport =>
            {
                if (deliveryReport.Error.IsError)
                {
                    _logger.LogError("Kafka delivery error to topic {Topic}: {Reason}", _topic, deliveryReport.Error.Reason);
                    // Note: Transport-level errors are logged but the Kafka client handles its own retries
                    // via MessageSendMaxRetries in ProducerConfig
                }
            });
        }

        /// <summary>
        /// Starts the sink operator.
        /// </summary>
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KafkaSinkOperator<TInput>));
            _isRunning = true;
            _logger.LogInformation("Kafka sink operator started for topic {Topic}", _topic);
        }

        /// <summary>
        /// Stops the sink operator and flushes pending messages.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _disposed)
                return;

            _isRunning = false;
            _logger.LogInformation("Stopping Kafka sink operator for topic {Topic}", _topic);

            try
            {
                // Flush with timeout to ensure pending messages are delivered
                var remaining = _producer.Flush(TimeSpan.FromSeconds(30));
                if (remaining > 0)
                {
                    _logger.LogWarning("{Remaining} messages were not delivered when stopping Kafka sink for topic {Topic}", remaining, _topic);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing Kafka producer for topic {Topic}", _topic);
            }

            Dispose();
        }

        /// <summary>
        /// Disposes the Kafka producer.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _producer?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Kafka producer for topic {Topic}", _topic);
            }

            _logger.LogInformation("Kafka sink operator disposed for topic {Topic}", _topic);
        }
    }
}
