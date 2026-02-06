using Cortex.Streams.ErrorHandling;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cortex.Streams.Operators
{
    /// <summary>
    /// Adapter that wraps an ISinkOperator to work within the operator chain.
    /// Forwards telemetry and error handling configuration to the wrapped operator.
    /// </summary>
    public class SinkOperatorAdapter<TInput> : IOperator, IHasNextOperators, ITelemetryEnabled, IErrorHandlingEnabled
    {
        private readonly ISinkOperator<TInput> _sinkOperator;

        // Cached operator name to avoid string allocation on hot path
        private static readonly string OperatorName = $"SinkOperatorAdapter<{typeof(TInput).Name}>";

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

        // Error handling fields
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        public SinkOperatorAdapter(ISinkOperator<TInput> sinkOperator)
        {
            _sinkOperator = sinkOperator ?? throw new ArgumentNullException(nameof(sinkOperator));
        }

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _processedCounter = metricsProvider.CreateCounter($"sink_operator_adapter_processed_{typeof(TInput).Name}", "Number of items processed by SinkOperatorAdapter");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"sink_operator_adapter_processing_time_{typeof(TInput).Name}", "Processing time for SinkOperatorAdapter");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"SinkOperatorAdapter_{typeof(TInput).Name}");

                // Cache delegates
                _incrementProcessedCounter = () => _processedCounter.Increment();
                _recordProcessingTime = value => _processingTimeHistogram.Record(value);
            }
            else
            {
                _incrementProcessedCounter = null;
                _recordProcessingTime = null;
            }
        }

        /// <summary>
        /// Forwards error handling configuration to the wrapped sink operator if it implements IErrorHandlingEnabled.
        /// Also stores the options for use in this adapter's error handling.
        /// </summary>
        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;

            // Forward error handling to the wrapped sink operator if it supports it
            if (_sinkOperator is IErrorHandlingEnabled errorHandlingEnabled)
            {
                errorHandlingEnabled.SetErrorHandling(options);
            }
        }

        public void Process(object input)
        {
            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();

                using (var span = _tracer.StartSpan("SinkOperatorAdapter.Process"))
                {
                    try
                    {
                        var executed = ErrorHandlingHelper.TryExecute<TInput>(
                            _executionOptions,
                            OperatorName,
                            input,
                            item => _sinkOperator.Process(item));

                        span.SetAttribute("status", executed ? "success" : "skipped");
                    }
                    catch (StreamStoppedException ex)
                    {
                        span.SetAttribute("status", "stopped");
                        span.SetAttribute("exception", ex.ToString());
                        throw;
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
                ErrorHandlingHelper.TryExecute<TInput>(
                    _executionOptions,
                    OperatorName,
                    input,
                    item => _sinkOperator.Process(item));
            }
        }

        public void SetNext(IOperator nextOperator)
        {
            // Sink operator is the end; does nothing
        }

        public IEnumerable<IOperator> GetNextOperators()
        {
            // Sink operator adapter has no next operator
            yield break;
        }
    }
}
