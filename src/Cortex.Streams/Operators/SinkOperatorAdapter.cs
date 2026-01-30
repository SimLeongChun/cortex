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

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

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
        /// </summary>
        public void SetErrorHandling(StreamExecutionOptions options)
        {
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
                        _sinkOperator.Process((TInput)input);
                        span.SetAttribute("status", "success");
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
                        _recordProcessingTime(stopwatch.Elapsed.TotalMilliseconds);
                        _incrementProcessedCounter();
                    }
                }
            }
            else
            {
                _sinkOperator.Process((TInput)input);
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
