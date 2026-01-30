using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.Http
{
    /// <summary>
    /// A sink operator that pushes data to an HTTP endpoint asynchronously.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">Type of data consumed by this sink.</typeparam>
    public class HttpSinkOperatorAsync<TInput> : ISinkOperator<TInput>, IErrorHandlingEnabled
    {
        private static readonly string OperatorName = $"HttpSinkOperatorAsync<{typeof(TInput).Name}>";

        private readonly string _endpoint;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<HttpSinkOperatorAsync<TInput>> _logger;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        // Internal queue and background worker
        private BlockingCollection<TInput> _messageQueue;
        private CancellationTokenSource _cts;
        private Task _workerTask;
        private bool _isRunning;

        /// <summary>
        /// Constructs an asynchronous HTTP sink operator.
        /// </summary>
        /// <param name="endpoint">The HTTP endpoint to which data should be posted.</param>
        /// <param name="httpClient">
        /// Optional <see cref="HttpClient"/>.  
        /// If null, a new HttpClient will be created (but consider <see cref="IHttpClientFactory"/> in production).
        /// </param>
        /// <param name="jsonOptions">Optional JSON serialization options.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        public HttpSinkOperatorAsync(
            string endpoint,
            HttpClient httpClient = null,
            JsonSerializerOptions jsonOptions = null,
            ILogger<HttpSinkOperatorAsync<TInput>> logger = null)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

            _httpClient = httpClient ?? new HttpClient();
            _jsonOptions = jsonOptions ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            _logger = logger ?? NullLogger<HttpSinkOperatorAsync<TInput>>.Instance;
        }

        /// <summary>
        /// Sets the stream-level error handling options.
        /// </summary>
        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;
        }

        /// <summary>
        /// Called once when the sink operator starts. Spawns a background worker.
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;

            // Prepare the queue and cancellation token
            _messageQueue = new BlockingCollection<TInput>(boundedCapacity: 10000);
            _cts = new CancellationTokenSource();

            // Launch the worker that processes messages asynchronously
            _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
            _isRunning = true;
            _logger.LogInformation("HttpSinkOperatorAsync started for endpoint {Endpoint}", _endpoint);
        }

        /// <summary>
        /// Queues incoming data for asynchronous sending.
        /// Uses stream-level error handling for queue operations.
        /// </summary>
        /// <param name="input">The data to be sent to the HTTP endpoint.</param>
        public void Process(TInput input)
        {
            if (!_isRunning)
            {
                _logger.LogWarning("HttpSinkOperatorAsync is not running. Call Start() before processing data.");
                return;
            }

            // Use core error handling for enqueueing
            ErrorHandlingHelper.TryExecute(
                _executionOptions,
                OperatorName,
                input,
                (Action<TInput>)EnqueueMessage);
        }

        private void EnqueueMessage(TInput input)
        {
            _messageQueue.Add(input);
        }

        /// <summary>
        /// Called once when the sink operator stops. Waits for the background worker to finish.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

            _cts.Cancel();
            _messageQueue.CompleteAdding();

            // Safely wait for the worker task to exit
            try
            {
                _workerTask.Wait();
            }
            catch (AggregateException ex)
            {
                _logger.LogWarning(ex, "HttpSinkOperatorAsync: Worker stopped with exception for endpoint {Endpoint}", _endpoint);
            }

            _cts.Dispose();
            _messageQueue.Dispose();
            _isRunning = false;
            _logger.LogInformation("HttpSinkOperatorAsync stopped for endpoint {Endpoint}", _endpoint);
        }

        /// <summary>
        /// Continuously consumes messages from the queue and sends them via HTTP asynchronously.
        /// </summary>
        private async Task WorkerLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && !_messageQueue.IsCompleted)
            {
                TInput item;
                try
                {
                    item = _messageQueue.Take(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                // Send the item asynchronously
                try
                {
                    await SendAsync(item, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "HttpSinkOperatorAsync: Error processing message for endpoint {Endpoint}", _endpoint);
                }
            }
        }

        /// <summary>
        /// Sends one item to the configured HTTP endpoint.
        /// </summary>
        private async Task SendAsync(TInput item, CancellationToken token)
        {
            var json = JsonSerializer.Serialize(item, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(_endpoint, content, token);
            response.EnsureSuccessStatusCode();

            _logger.LogDebug("Successfully posted data to {Endpoint}", _endpoint);
        }
    }
}
