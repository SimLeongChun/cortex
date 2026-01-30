using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Cortex.Streams.RabbitMQ.Serializers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Cortex.Streams.RabbitMQ
{
    /// <summary>
    /// RabbitMQ Sink Operator with serialization support, connection recovery, and SSL configuration.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of objects to send.</typeparam>
    public class RabbitMQSinkOperator<TInput> : ISinkOperator<TInput>, IErrorHandlingEnabled, IDisposable
    {
        private static readonly string OperatorName = $"RabbitMQSinkOperator<{typeof(TInput).Name}>";

        private readonly string _hostname;
        private readonly int _port;
        private readonly string _queueName;
        private readonly string _username;
        private readonly string _password;
        private readonly string _virtualHost;
        private readonly bool _useSsl;
        private readonly ISerializer<TInput> _serializer;
        private readonly ILogger<RabbitMQSinkOperator<TInput>> _logger;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;
        private IConnection _connection;
        private IModel _channel;
        private bool _isRunning;
        private bool _disposed;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitMQSinkOperator{TInput}"/> class.
        /// </summary>
        /// <param name="hostname">The RabbitMQ server hostname.</param>
        /// <param name="queueName">The name of the RabbitMQ queue to publish to.</param>
        /// <param name="username">The RabbitMQ username. Avoid using 'guest' in production.</param>
        /// <param name="password">The RabbitMQ password. Avoid using 'guest' in production.</param>
        /// <param name="serializer">The serializer to convert TInput objects to strings.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="port">The RabbitMQ server port. Default is 5672 (5671 for SSL).</param>
        /// <param name="virtualHost">The RabbitMQ virtual host. Default is "/".</param>
        /// <param name="useSsl">Whether to use SSL/TLS. Default is false.</param>
        public RabbitMQSinkOperator(
            string hostname,
            string queueName,
            string username = null,
            string password = null,
            ISerializer<TInput> serializer = null,
            ILogger<RabbitMQSinkOperator<TInput>> logger = null,
            int port = 5672,
            string virtualHost = "/",
            bool useSsl = false)
        {
            _hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
            _queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
            _port = port;
            _virtualHost = virtualHost;
            _useSsl = useSsl;

            // Warn about default credentials
            if (string.IsNullOrEmpty(username) || username == "guest")
            {
                _username = username ?? "guest";
                _logger?.LogWarning("Using default 'guest' credentials is not recommended for production environments");
            }
            else
            {
                _username = username;
            }

            _password = password ?? "guest";
            _serializer = serializer ?? new DefaultJsonSerializer<TInput>();
            _logger = logger ?? NullLogger<RabbitMQSinkOperator<TInput>>.Instance;

            InitializeConnection();
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

        private void InitializeConnection()
        {
            var factory = new ConnectionFactory()
            {
                HostName = _hostname,
                Port = _port,
                UserName = _username,
                Password = _password,
                VirtualHost = _virtualHost,
                // Connection recovery settings
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                // Connection timeout settings
                RequestedConnectionTimeout = TimeSpan.FromSeconds(30),
                SocketReadTimeout = TimeSpan.FromSeconds(30),
                SocketWriteTimeout = TimeSpan.FromSeconds(30),
            };

            // Configure SSL if enabled
            if (_useSsl)
            {
                factory.Ssl = new SslOption
                {
                    Enabled = true,
                    ServerName = _hostname
                };
            }

            _connection = factory.CreateConnection();
            _connection.ConnectionShutdown += OnConnectionShutdown;
            _connection.ConnectionBlocked += OnConnectionBlocked;
            _connection.ConnectionUnblocked += OnConnectionUnblocked;

            _channel = _connection.CreateModel();
            _channel.ConfirmSelect(); // Enable publisher confirms

            _channel.QueueDeclare(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _logger.LogInformation("RabbitMQ connection established to {Hostname}:{Port}, queue {QueueName}",
                _hostname, _port, _queueName);
        }

        private void OnConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection shutdown: {ReplyText}", e.ReplyText);
        }

        private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection blocked: {Reason}", e.Reason);
        }

        private void OnConnectionUnblocked(object sender, EventArgs e)
        {
            _logger.LogInformation("RabbitMQ connection unblocked");
        }

        /// <summary>
        /// Starts the sink operator.
        /// </summary>
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMQSinkOperator<TInput>));
            if (_isRunning) return;

            _isRunning = true;
            _logger.LogInformation("RabbitMQ sink operator started for queue {QueueName}", _queueName);
        }

        /// <summary>
        /// Processes the input object by serializing it and sending it to RabbitMQ.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The input object to send.</param>
        public void Process(TInput input)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMQSinkOperator<TInput>));
            if (!_isRunning)
            {
                _logger.LogWarning("RabbitMQSinkOperator is not running. Call Start() before processing messages.");
                return;
            }

            if (input == null)
            {
                _logger.LogDebug("RabbitMQSinkOperator received null input. Skipping.");
                return;
            }

            // Use core error handling for message processing
            ErrorHandlingHelper.TryExecute(
                _executionOptions,
                OperatorName,
                input,
                (Action<TInput>)SendMessage);
        }

        private void SendMessage(TInput obj)
        {
            var serializedMessage = _serializer.Serialize(obj);
            var body = Encoding.UTF8.GetBytes(serializedMessage);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            lock (_lock)
            {
                _channel.BasicPublish(
                    exchange: "",
                    routingKey: _queueName,
                    basicProperties: properties,
                    body: body);

                // Wait for confirmation
                _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
            }
        }

        /// <summary>
        /// Stops the sink operator.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _disposed) return;

            _logger.LogInformation("Stopping RabbitMQ sink operator for queue {QueueName}", _queueName);
            _isRunning = false;

            Dispose();
            _logger.LogInformation("RabbitMQ sink operator stopped for queue {QueueName}", _queueName);
        }

        /// <summary>
        /// Disposes the RabbitMQ connection and channel.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            lock (_lock)
            {
                _disposed = true;

                try
                {
                    if (_channel?.IsOpen == true)
                    {
                        _channel.Close();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing RabbitMQ channel for queue {QueueName}", _queueName);
                }

                try
                {
                    if (_connection?.IsOpen == true)
                    {
                        _connection.Close();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing RabbitMQ connection for queue {QueueName}", _queueName);
                }

                try
                {
                    _channel?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing RabbitMQ channel for queue {QueueName}", _queueName);
                }

                try
                {
                    _connection?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing RabbitMQ connection for queue {QueueName}", _queueName);
                }
            }
        }
    }
}
