using Cortex.Streams.ErrorHandling;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cortex.Streams.Operators
{
    /// <summary>
    /// An operator that consumes data at the end of the stream.
    /// </summary>
    /// <typeparam name="TInput">The type of data consumed by the sink.</typeparam>
    public class SinkOperator<TInput> : IOperator, IHasNextOperators, ITelemetryEnabled, IErrorHandlingEnabled
    {
        private readonly Action<TInput> _sinkFunction;

        // Cached operator name to avoid string allocation on hot path
        private static readonly string OperatorName = $"SinkOperator<{typeof(TInput).Name}>";

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;

        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        public SinkOperator(Action<TInput> sinkFunction)
        {
            _sinkFunction = sinkFunction;
        }

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _processedCounter = metricsProvider.CreateCounter($"sink_operator_processed_{typeof(TInput).Name}", "Number of items processed by SinkOperator");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"sink_operator_processing_time_{typeof(TInput).Name}", "Processing time for SinkOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"SinkOperator_{typeof(TInput).Name}");

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

        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;
        }

        public void Process(object input)
        {
            TInput typedInput = (TInput)input;

            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();
                using (var span = _tracer.StartSpan("SinkOperator.Process"))
                {
                    try
                    {
                        var executed = ErrorHandlingHelper.TryExecute<TInput>(
                            _executionOptions,
                            OperatorName,
                            input,
                            _sinkFunction);

                        span.SetAttribute("status", executed ? "success" : "skipped");
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
                    _sinkFunction);
            }
        }

        public void SetNext(IOperator nextOperator)
        {
            // Sink operator is terminal; no next operator.
        }

        public IEnumerable<IOperator> GetNextOperators()
        {
            yield break;
        }
    }
}
