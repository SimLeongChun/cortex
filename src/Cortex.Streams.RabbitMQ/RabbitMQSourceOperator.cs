using Cortex.Streams.Operators;
using Cortex.Streams.RabbitMQ.Deserializers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.RabbitMQ
{
    /// <summary>
    /// RabbitMQ Source Operator with deserialization support, connection recovery, and SSL configuration.
    /// </summary>
    /// <typeparam name="TOutput">The type of objects to emit.</typeparam>
    public class RabbitMQSourceOperator<TOutput> : ISourceOperator<TOutput>, IDisposable
    {
        private readonly string _hostname;
        private readonly int _port;
        private readonly string _queueName;
        private readonly string _username;
        private readonly string _password;
        private readonly string _virtualHost;
        private readonly bool _useSsl;
        private readonly IDeserializer<TOutput> _deserializer;
        private readonly ILogger<RabbitMQSourceOperator<TOutput>> _logger;
        private readonly Action<Exception, BasicDeliverEventArgs> _errorHandler;
        private readonly ushort _prefetchCount;
        private readonly int _maxRetries;
        private readonly TimeSpan _retryDelay;
        private IConnection _connection;
        private IModel _channel;
        private AsyncEventingBasicConsumer _consumer;
        private Action<TOutput> _emitAction;
        private bool _isRunning;
        private bool _disposed;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitMQSourceOperator{TOutput}"/> class.
        /// </summary>
        /// <param name="hostname">The RabbitMQ server hostname.</param>
        /// <param name="queueName">The name of the RabbitMQ queue to consume from.</param>
        /// <param name="username">The RabbitMQ username. Avoid using 'guest' in production.</param>
        /// <param name="password">The RabbitMQ password. Avoid using 'guest' in production.</param>
        /// <param name="deserializer">The deserializer to convert message strings to TOutput objects.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <param name="port">The RabbitMQ server port. Default is 5672 (5671 for SSL).</param>
        /// <param name="virtualHost">The RabbitMQ virtual host. Default is "/".</param>
        /// <param name="useSsl">Whether to use SSL/TLS. Default is false.</param>
        /// <param name="prefetchCount">Number of messages to prefetch. Default is 10.</param>
        /// <param name="errorHandler">Optional error handler for processing failures.</param>
        /// <param name="maxRetries">Maximum number of connection retry attempts. Default is 5.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between connection retries. Default is 5000ms.</param>
        public RabbitMQSourceOperator(
            string hostname,
            string queueName,
            string username = null,
            string password = null,
            IDeserializer<TOutput> deserializer = null,
            ILogger<RabbitMQSourceOperator<TOutput>> logger = null,
            int port = 5672,
            string virtualHost = "/",
            bool useSsl = false,
            ushort prefetchCount = 10,
            Action<Exception, BasicDeliverEventArgs> errorHandler = null,
            int maxRetries = 5,
            int retryDelayMs = 5000)
        {
            _hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
            _queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
            _port = port;
            _virtualHost = virtualHost;
            _useSsl = useSsl;
            _prefetchCount = prefetchCount;
            _errorHandler = errorHandler;
            _maxRetries = maxRetries;
            _retryDelay = TimeSpan.FromMilliseconds(retryDelayMs);

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
            _deserializer = deserializer ?? new DefaultJsonDeserializer<TOutput>();
            _logger = logger ?? NullLogger<RabbitMQSourceOperator<TOutput>>.Instance;

            InitializeConnection();
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
                DispatchConsumersAsync = true,
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

            Exception lastException = null;
            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    _connection = factory.CreateConnection();
                    _connection.ConnectionShutdown += OnConnectionShutdown;
                    _connection.ConnectionBlocked += OnConnectionBlocked;
                    _connection.ConnectionUnblocked += OnConnectionUnblocked;

                    _channel = _connection.CreateModel();
                    _channel.BasicQos(0, _prefetchCount, false);

                    _channel.QueueDeclare(
                        queue: _queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    _logger.LogInformation("RabbitMQ connection established to {Hostname}:{Port}, queue {QueueName}",
                        _hostname, _port, _queueName);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Failed to connect to RabbitMQ, attempt {Attempt} of {MaxRetries}",
                        attempt + 1, _maxRetries + 1);

                    if (attempt < _maxRetries)
                    {
                        Thread.Sleep(_retryDelay);
                    }
                }
            }

            throw new InvalidOperationException($"Failed to connect to RabbitMQ after {_maxRetries + 1} attempts", lastException);
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
        /// Starts the source operator.
        /// </summary>
        /// <param name="emit">The action to emit deserialized objects.</param>
        public void Start(Action<TOutput> emit)
        {
            if (emit == null) throw new ArgumentNullException(nameof(emit));
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMQSourceOperator<TOutput>));
            if (_isRunning) throw new InvalidOperationException("RabbitMQSourceOperator is already running.");

            lock (_lock)
            {
                _emitAction = emit;
                _consumer = new AsyncEventingBasicConsumer(_channel);
                _consumer.Received += OnMessageReceivedAsync;

                _channel.BasicConsume(
                    queue: _queueName,
                    autoAck: false,
                    consumer: _consumer);

                _isRunning = true;
                _logger.LogInformation("RabbitMQ source operator started for queue {QueueName}", _queueName);
            }
        }

        private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var obj = _deserializer.Deserialize(message);

                _emitAction?.Invoke(obj);

                // Acknowledge the message
                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from RabbitMQ queue {QueueName}", _queueName);
                _errorHandler?.Invoke(ex, ea);

                try
                {
                    // Reject and do not requeue to avoid infinite loops - send to dead letter queue if configured
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
                catch (Exception nackEx)
                {
                    _logger.LogError(nackEx, "Failed to NACK message from RabbitMQ queue {QueueName}", _queueName);
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Stops the source operator.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _disposed) return;

            lock (_lock)
            {
                _logger.LogInformation("Stopping RabbitMQ source operator for queue {QueueName}", _queueName);

                try
                {
                    if (_consumer?.ConsumerTags?.Length > 0)
                    {
                        foreach (var tag in _consumer.ConsumerTags)
                        {
                            try
                            {
                                _channel?.BasicCancel(tag);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error canceling consumer tag {Tag}", tag);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping RabbitMQ consumer for queue {QueueName}", _queueName);
                }

                _isRunning = false;
            }

            Dispose();
            _logger.LogInformation("RabbitMQ source operator stopped for queue {QueueName}", _queueName);
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
