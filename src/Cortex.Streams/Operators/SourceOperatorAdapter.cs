using Cortex.Streams.ErrorHandling;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cortex.Streams.Operators
{
    /// <summary>
    /// Adapter that wraps an ISourceOperator to work within the operator chain.
    /// Handles telemetry, error handling, and lifecycle management for source operators.
    /// </summary>
    public class SourceOperatorAdapter<TOutput> : IOperator, IHasNextOperators, ITelemetryEnabled, IErrorHandlingEnabled
    {
        private readonly ISourceOperator<TOutput> _sourceOperator;
        private IOperator _nextOperator;

        // Cached operator name to avoid string allocation on hot path
        private static readonly string OperatorName = $"SourceOperatorAdapter<{typeof(TOutput).Name}>";

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _emittedCounter;
        private IHistogram _emissionTimeHistogram;
        private ITracer _tracer;
        private Action _incrementEmittedCounter;
        private Action<double> _recordEmissionTime;

        // Error handling fields
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        public SourceOperatorAdapter(ISourceOperator<TOutput> sourceOperator)
        {
            _sourceOperator = sourceOperator;
        }

        /// <summary>
        /// Sets the error handling options for this operator and propagates to next operators.
        /// </summary>
        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;

            // Propagate to the next operator if it supports error handling
            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }
        }

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _emittedCounter = metricsProvider.CreateCounter($"source_operator_emitted_{typeof(TOutput).Name}", "Number of items emitted by SourceOperator");
                _emissionTimeHistogram = metricsProvider.CreateHistogram($"source_operator_emission_time_{typeof(TOutput).Name}", "Emission time for SourceOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"SourceOperator_{typeof(TOutput).Name}");

                // Cache delegates
                _incrementEmittedCounter = () => _emittedCounter.Increment();
                _recordEmissionTime = value => _emissionTimeHistogram.Record(value);
            }
            else
            {
                _incrementEmittedCounter = null;
                _recordEmissionTime = null;
            }

            // Propagate telemetry to the next operator
            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled)
            {
                nextTelemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }
        }

        public void Process(object input)
        {
            // Not used in source operator
        }

        public void SetNext(IOperator nextOperator)
        {
            _nextOperator = nextOperator;

            // Propagate telemetry to the next operator
            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled && _telemetryProvider != null)
            {
                nextTelemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }

            // Propagate error handling to the next operator
            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling && _executionOptions != null)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }

            // Start the source operator
            Start();
        }

        private void Start()
        {
            _sourceOperator.Start(output =>
            {
                if (_telemetryProvider != null)
                {
                    var stopwatch = Stopwatch.StartNew();

                    using (var span = _tracer.StartSpan("SourceOperator.Emit"))
                    {
                        try
                        {
                            _incrementEmittedCounter?.Invoke();

                            // Use error handling helper to properly handle errors according to stream configuration
                            var executed = ErrorHandlingHelper.TryExecute<TOutput>(
                                _executionOptions,
                                OperatorName,
                                output,
                                item => _nextOperator?.Process(item));

                            span.SetAttribute("status", executed ? "success" : "skipped");
                        }
                        catch (StreamStoppedException ex)
                        {
                            span.SetAttribute("status", "stopped");
                            span.SetAttribute("exception", ex.ToString());

                            // Graceful stop: stop the source and do not rethrow.
                            Stop();
                        }
                        catch (Exception ex)
                        {
                            span.SetAttribute("status", "error");
                            span.SetAttribute("exception", ex.ToString());
                            // Re-throw to let the source operator handle it (e.g., logging, stopping)
                            throw;
                        }
                        finally
                        {
                            stopwatch.Stop();
                            _recordEmissionTime?.Invoke(stopwatch.Elapsed.TotalMilliseconds);
                        }
                    }
                }
                else
                {
                    try
                    {
                        // Use error handling helper to properly handle errors according to stream configuration
                        ErrorHandlingHelper.TryExecute<TOutput>(
                            _executionOptions,
                            OperatorName,
                            output,
                            item => _nextOperator?.Process(item));
                    }
                    catch (StreamStoppedException)
                    {
                        Stop();
                    }
                }
            });
        }

        public void Stop()
        {
            _sourceOperator.Stop();
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
