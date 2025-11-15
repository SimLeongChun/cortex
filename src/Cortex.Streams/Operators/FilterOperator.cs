using Cortex.Streams.ErrorHandling;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cortex.Streams.Operators
{
    /// <summary>
    /// An operator that filters data based on a predicate.
    /// </summary>
    /// <typeparam name="T">The type of data being filtered.</typeparam>
    public class FilterOperator<T> : IOperator, IHasNextOperators, ITelemetryEnabled, IErrorHandlingEnabled
    {
        private readonly Func<T, bool> _predicate;
        private IOperator _nextOperator;

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private ICounter _filteredOutCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;

        private Action _incrementProcessedCounter;
        private Action _incrementFilteredOutCounter;
        private Action<double> _recordProcessingTime;

        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;


        public FilterOperator(Func<T, bool> predicate)
        {
            _predicate = predicate;
        }

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _processedCounter = metricsProvider.CreateCounter($"filter_operator_processed_{typeof(T).Name}", "Number of items processed by FilterOperator");
                _filteredOutCounter = metricsProvider.CreateCounter($"filter_operator_filtered_out_{typeof(T).Name}", "Number of items filtered out by FilterOperator");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"filter_operator_processing_time_{typeof(T).Name}", "Processing time for FilterOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"FilterOperator_{typeof(T).Name}");

                // Cache delegates
                _incrementProcessedCounter = () => _processedCounter.Increment();
                _incrementFilteredOutCounter = () => _filteredOutCounter.Increment();
                _recordProcessingTime = value => _processingTimeHistogram.Record(value);
            }
            else
            {
                _incrementProcessedCounter = null;
                _incrementFilteredOutCounter = null;
                _recordProcessingTime = null;
            }

            // Propagate telemetry to the next operator
            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled)
            {
                nextTelemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }
        }

        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;

            // Propagate to the next operator if it supports error handling
            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }
        }

        public void Process(object input)
        {
            if (!(input is T typedInput))
                throw new ArgumentException($"Expected input of type {typeof(T).Name}, but received {input?.GetType().Name ?? "null"}");

            var operatorName = $"FilterOperator<{typeof(T).Name}>";

            bool isPassed = false;
            bool executedSuccessfully;

            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();
                using (var span = _tracer.StartSpan("FilterOperator.Process"))
                {
                    try
                    {
                        executedSuccessfully = ErrorHandlingHelper.TryExecute<T, bool>(
                            _executionOptions,
                            operatorName,
                            input,
                            _predicate,
                            typedInput,
                            out isPassed);

                        span.SetAttribute("filter_result", executedSuccessfully ? isPassed.ToString() : "skipped");
                        span.SetAttribute("status", executedSuccessfully ? "success" : "skipped");
                    }
                    catch (Exception ex)
                    {
                        span.SetAttribute("status", "error");
                        span.SetAttribute("exception", ex.ToString());
                        throw;
                    }
                    finally
                    {
                        stopwatch.Stop();
                        _recordProcessingTime?.Invoke(stopwatch.Elapsed.TotalMilliseconds);
                        _incrementProcessedCounter?.Invoke();
                    }
                }
            }
            else
            {
                executedSuccessfully = ErrorHandlingHelper.TryExecute<T, bool>(
                    _executionOptions,
                    operatorName,
                    input,
                    _predicate,
                    typedInput,
                    out isPassed);
            }

            if (!executedSuccessfully)
            {
                // treated as filtered-out on error skip
                _incrementFilteredOutCounter?.Invoke();
                return;
            }

            if (isPassed)
            {
                _nextOperator?.Process(input);
            }
            else
            {
                _incrementFilteredOutCounter?.Invoke();
            }
        }

        public void SetNext(IOperator nextOperator)
        {
            _nextOperator = nextOperator;

            // Propagate telemetry to the next operator
            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled && _telemetryProvider != null)
            {
                nextTelemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }

            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }
        }

        public IEnumerable<IOperator> GetNextOperators()
        {
            if (_nextOperator != null)
            {
                yield return _nextOperator;
            }
        }
    }
}
