using Cortex.Streams.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.Http
{
    /// <summary>
    /// A source operator that periodically calls an HTTP endpoint and emits the response into the stream.
    /// </summary>
    /// <typeparam name="TOutput">The type of data emitted by the HTTP source operator.</typeparam>
    public class HttpSourceOperator<TOutput> : ISourceOperator<TOutput>
    {
        private readonly string _endpoint;
        private readonly TimeSpan _pollInterval;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<HttpSourceOperator<TOutput>> _logger;
        private Timer _timer;
        private CancellationTokenSource _cts;

        // Retry configuration
        private readonly int _maxRetries;
        private readonly TimeSpan _initialDelay;

        /// <summary>
        /// Creates a new HttpSourceOperator.
        /// </summary>
        /// <param name="endpoint">The HTTP endpoint to call.</param>
        /// <param name="pollInterval">How often the endpoint should be polled.</param>
        /// <param name="maxRetries">Number of max consecutive retries on failure before giving up.</param>
        /// <param name="initialDelay">Initial backoff delay when retrying.</param>
        /// <param name="httpClient">Optional HttpClient. If null, a new HttpClient will be created.</param>
        /// <param name="jsonOptions">Optional JsonSerializerOptions for parsing JSON.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        public HttpSourceOperator(
            string endpoint,
            TimeSpan pollInterval,
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            HttpClient httpClient = null,
            JsonSerializerOptions jsonOptions = null,
            ILogger<HttpSourceOperator<TOutput>> logger = null)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _pollInterval = pollInterval;
            _maxRetries = maxRetries;
            _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(500);

            _httpClient = httpClient ?? new HttpClient();
            _jsonOptions = jsonOptions ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _logger = logger ?? NullLogger<HttpSourceOperator<TOutput>>.Instance;
        }

        /// <summary>
        /// Starts the source operator, periodically calling the endpoint and emitting data.
        /// </summary>
        /// <param name="emit">Action that receives the deserialized data.</param>
        public void Start(Action<TOutput> emit)
        {
            _cts = new CancellationTokenSource();

            // Create a timer that fires at the given interval
            _timer = new Timer(async _ => await PollAndEmitAsync(emit, _cts.Token), null, TimeSpan.Zero, _pollInterval);
        }

        /// <summary>
        /// Stops polling the endpoint.
        /// </summary>
        public void Stop()
        {
            _cts.Cancel();
            _timer?.Dispose();
        }

        /// <summary>
        /// Calls the configured HTTP endpoint, deserializes the result, and emits it.
        /// Includes simple retry with exponential backoff.
        /// </summary>
        private async Task PollAndEmitAsync(Action<TOutput> emit, CancellationToken token)
        {
            int attempt = 0;
            TimeSpan delay = _initialDelay;

            while (true)
            {
                if (token.IsCancellationRequested)
                    return;

                try
                {
                    using var response = await _httpClient.GetAsync(_endpoint, token);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(token);
                    var data = JsonSerializer.Deserialize<TOutput>(content, _jsonOptions);
                    emit(data);

                    // If we succeed, break out of the retry loop
                    break;
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt > _maxRetries)
                    {
                        _logger.LogError(ex, "HttpSourceOperator: Exhausted {MaxRetries} retries for endpoint {Endpoint}", _maxRetries, _endpoint);
                        break;
                    }

                    // Exponential backoff
                    _logger.LogWarning(ex, "HttpSourceOperator: Error calling HTTP endpoint {Endpoint} (attempt {Attempt} of {MaxRetries}). Retrying in {Delay}", _endpoint, attempt, _maxRetries, delay);
                    await Task.Delay(delay, token);

                    // Increase the delay
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
                }
            }
        }
    }
}
