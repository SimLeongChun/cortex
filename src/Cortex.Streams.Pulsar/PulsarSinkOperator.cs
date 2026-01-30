using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using DotPulsar.Abstractions;
using DotPulsar;
using Cortex.Streams.Pulsar.Serializers;
using DotPulsar.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Buffers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.Pulsar
{
    /// <summary>
    /// Pulsar sink operator that produces messages to a Pulsar topic.
    /// Supports authentication and proper resource management.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of objects to send.</typeparam>
    public class PulsarSinkOperator<TInput> : ISinkOperator<TInput>, IErrorHandlingEnabled, IDisposable
    {
        private static readonly string OperatorName = $"PulsarSinkOperator<{typeof(TInput).Name}>";

        private readonly string _serviceUrl;
        private readonly string _topic;
        private readonly ISerializer<TInput> _serializer;
        private readonly IPulsarClient _client;
        private readonly ILogger<PulsarSinkOperator<TInput>> _logger;
        private readonly Func<TInput, string> _keySelector;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;
        private IProducer<ReadOnlySequence<byte>> _producer;
        private bool _disposed;
        private bool _isRunning;

        /// <summary>
        /// Initializes a new instance of the <see cref="PulsarSinkOperator{TInput}"/> class.
        /// </summary>
        /// <param name="serviceUrl">The Pulsar service URL.</param>
        /// <param name="topic">The topic to produce to.</param>
        /// <param name="keySelector">Optional function to extract message key from input.</param>
        /// <param name="serializer">The serializer to convert TInput objects to bytes.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="authenticationToken">Optional authentication token for secure connections.</param>
        public PulsarSinkOperator(
            string serviceUrl,
            string topic,
            Func<TInput, string> keySelector = null,
            ISerializer<TInput> serializer = null,
            ILogger<PulsarSinkOperator<TInput>> logger = null,
            string authenticationToken = null)
        {
            _serviceUrl = serviceUrl ?? throw new ArgumentNullException(nameof(serviceUrl));
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _serializer = serializer ?? new DefaultJsonSerializer<TInput>();
            _logger = logger ?? NullLogger<PulsarSinkOperator<TInput>>.Instance;
            _keySelector = keySelector;

            var clientBuilder = PulsarClient.Builder()
                .ServiceUrl(new Uri(_serviceUrl));

            // Add authentication if provided
            if (!string.IsNullOrEmpty(authenticationToken))
            {
                clientBuilder.Authentication(AuthenticationFactory.Token(authenticationToken));
            }

            _client = clientBuilder.Build();

            // Start producer immediately as per BUG #103
            Start();
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
        /// Starts the sink operator and creates the producer.
        /// </summary>
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PulsarSinkOperator<TInput>));
            if (_isRunning) return;

            _producer = _client.NewProducer()
                .Topic(_topic)
                .Create();

            _isRunning = true;
            _logger.LogInformation("Pulsar sink operator started for topic {Topic}", _topic);
        }

        /// <summary>
        /// Processes the input object by serializing it and sending it to the Pulsar topic.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The input object to send.</param>
        public void Process(TInput input)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PulsarSinkOperator<TInput>));
            if (!_isRunning)
            {
                _logger.LogWarning("PulsarSinkOperator is not running. Call Start() before processing messages.");
                return;
            }

            if (input == null)
            {
                _logger.LogDebug("PulsarSinkOperator received null input. Skipping.");
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
            var data = _serializer.Serialize(input);
            if (_keySelector is null)
            {
                _producer.Send(data);
            }
            else
            {
                var metadata = new MessageMetadata { Key = _keySelector(input) };
                _producer.Send(metadata, new ReadOnlySequence<byte>(data));
        }
        }

        /// <summary>
        /// Stops the sink operator and releases resources.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _disposed)
                return;

            _isRunning = false;
            _logger.LogInformation("Stopping Pulsar sink operator for topic {Topic}", _topic);

            Dispose();
        }

        /// <summary>
        /// Disposes the Pulsar producer and client.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                // Use ConfigureAwait(false) to avoid deadlocks
                _producer?.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Pulsar producer for topic {Topic}", _topic);
            }

            try
            {
                _client?.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Pulsar client for topic {Topic}", _topic);
            }

            _logger.LogInformation("Pulsar sink operator disposed for topic {Topic}", _topic);
        }
    }
}
