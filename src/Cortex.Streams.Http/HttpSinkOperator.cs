using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Cortex.Streams.Http
{
    /// <summary>
    /// A sink operator that pushes data to an HTTP endpoint.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of data consumed by the HTTP sink operator.</typeparam>
    public class HttpSinkOperator<TInput> : ISinkOperator<TInput>, IErrorHandlingEnabled
    {
        private static readonly string OperatorName = $"HttpSinkOperator<{typeof(TInput).Name}>";

        private readonly string _endpoint;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<HttpSinkOperator<TInput>> _logger;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;
        private bool _isRunning;

        /// <summary>
        /// Creates a new HttpSinkOperator.
        /// </summary>
        /// <param name="endpoint">The endpoint where data should be posted.</param>
        /// <param name="httpClient">Optional HttpClient. If null, a new HttpClient will be created.</param>
        /// <param name="jsonOptions">Optional JsonSerializerOptions for serializing JSON.</param>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        public HttpSinkOperator(
            string endpoint,
            HttpClient httpClient = null,
            JsonSerializerOptions jsonOptions = null,
            ILogger<HttpSinkOperator<TInput>> logger = null)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

            _httpClient = httpClient ?? new HttpClient();
            _jsonOptions = jsonOptions ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            _logger = logger ?? NullLogger<HttpSinkOperator<TInput>>.Instance;
        }

        /// <summary>
        /// Sets the stream-level error handling options.
        /// </summary>
        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;
        }

        /// <summary>
        /// Called once when the sink operator is started.
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _logger.LogInformation("HttpSinkOperator started for endpoint {Endpoint}", _endpoint);
        }

        /// <summary>
        /// Processes each incoming item from the stream, pushing it to the HTTP endpoint.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The data to send.</param>
        public void Process(TInput input)
        {
            if (!_isRunning)
            {
                _logger.LogWarning("HttpSinkOperator is not running. Call Start() before processing data.");
                return;
            }

            // Use core error handling for HTTP post operations
            ErrorHandlingHelper.TryExecute(
                _executionOptions,
                OperatorName,
                input,
                (Action<TInput>)PostToEndpoint);
        }

        private void PostToEndpoint(TInput input)
        {
            var json = JsonSerializer.Serialize(input, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = _httpClient.PostAsync(_endpoint, content).Result;
            response.EnsureSuccessStatusCode();

            _logger.LogDebug("Successfully posted data to {Endpoint}", _endpoint);
        }

        /// <summary>
        /// Called once when the sink operator is stopped.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _logger.LogInformation("HttpSinkOperator stopped for endpoint {Endpoint}", _endpoint);
        }
    }
}