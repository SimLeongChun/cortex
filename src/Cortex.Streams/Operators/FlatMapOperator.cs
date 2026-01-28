using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cortex.Telemetry;
using Cortex.Streams.ErrorHandling;

namespace Cortex.Streams.Operators
{
    /// <summary>
    /// The FlatMapOperator takes each input element, applies a function to produce zero or more output elements, 
    /// and emits each output element individually into the stream.
    /// </summary>
    /// <typeparam name="TInput">The type of the input element.</typeparam>
    /// <typeparam name="TOutput">The type of the output element(s) produced.</typeparam>
    public class FlatMapOperator<TInput, TOutput> :
        IOperator,
        IHasNextOperators,
        ITelemetryEnabled,
        IErrorHandlingEnabled
    {
        private readonly Func<TInput, IEnumerable<TOutput>> _flatMapFunction;
        private IOperator _nextOperator;

        // Cached operator name to avoid string allocation on hot path
        private static readonly string OperatorName = $"FlatMapOperator<{typeof(TInput).Name},{typeof(TOutput).Name}>";
        private static readonly string InputTypeName = typeof(TInput).Name;

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private ICounter _emittedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action _incrementEmittedCounter;
        private Action<double> _recordProcessingTime;

        // Global error handling
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        public FlatMapOperator(Func<TInput, IEnumerable<TOutput>> flatMapFunction)
        {
            _flatMapFunction = flatMapFunction ?? throw new ArgumentNullException(nameof(flatMapFunction));
        }

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metrics = _telemetryProvider.GetMetricsProvider();

                _processedCounter = metrics.CreateCounter(
                    $"flatmap_operator_processed_{typeof(TInput).Name}_to_{typeof(TOutput).Name}",
                    "Number of items processed by FlatMapOperator");

                _emittedCounter = metrics.CreateCounter(
                    $"flatmap_operator_emitted_{typeof(TInput).Name}_to_{typeof(TOutput).Name}",
                    "Number of items emitted by FlatMapOperator");

                _processingTimeHistogram = metrics.CreateHistogram(
                    $"flatmap_operator_processing_time_{typeof(TInput).Name}_to_{typeof(TOutput).Name}",
                    "Processing time for FlatMapOperator");

                _tracer = _telemetryProvider
                    .GetTracingProvider()
                    .GetTracer($"FlatMapOperator_{typeof(TInput).Name}_to_{typeof(TOutput).Name}");

                _incrementProcessedCounter = () => _processedCounter.Increment();
                _incrementEmittedCounter = () => _emittedCounter.Increment();
                _recordProcessingTime = value => _processingTimeHistogram.Record(value);
            }
            else
            {
                _processedCounter = null;
                _emittedCounter = null;
                _processingTimeHistogram = null;
                _tracer = null;

                _incrementProcessedCounter = null;
                _incrementEmittedCounter = null;
                _recordProcessingTime = null;
            }

            // Propagate telemetry to next operator
            if (_nextOperator is ITelemetryEnabled telemetryEnabled)
            {
                telemetryEnabled.SetTelemetryProvider(telemetryProvider);
            }
        }

        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;

            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }
        }

        public void Process(object input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            TInput typedInput;
            try
            {
                typedInput = (TInput)input;
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException(
                    $"Expected input of type {InputTypeName}, but received {input?.GetType().Name ?? "null"}");
            }

            bool executedSuccessfully;
            IEnumerable<TOutput> outputs = Array.Empty<TOutput>();

            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();
                using (var span = _tracer.StartSpan("FlatMapOperator.Process"))
                {
                    try
                    {
                        executedSuccessfully = ErrorHandlingHelper.TryExecute<TInput, IEnumerable<TOutput>>(
                            _executionOptions,
                            OperatorName,
                            input,
                            current => _flatMapFunction(current) ?? Array.Empty<TOutput>(),
                            typedInput,
                            out outputs);

                        span.SetAttribute("status", executedSuccessfully ? "success" : "skipped");
                        span.SetAttribute("flatmap_output_count", (executedSuccessfully ? ((outputs as ICollection<TOutput>)?.Count ?? -1) : -1).ToString());
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
                executedSuccessfully = ErrorHandlingHelper.TryExecute<TInput, IEnumerable<TOutput>>(
                    _executionOptions,
                    OperatorName,
                    input,
                    current => _flatMapFunction(current) ?? Array.Empty<TOutput>(),
                    typedInput,
                    out outputs);
            }

            // If the global error handling decided to Skip this element, do nothing
            if (!executedSuccessfully)
                return;

            // Emit downstream
            foreach (var output in outputs)
            {
                _nextOperator?.Process(output);
                _incrementEmittedCounter?.Invoke();
            }
        }

        public void SetNext(IOperator nextOperator)
        {
            _nextOperator = nextOperator;

            // Telemetry → downstream
            if (_nextOperator is ITelemetryEnabled telemetryEnabled && _telemetryProvider != null)
            {
                telemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }

            // Error handling → downstream
            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }
        }

        public IEnumerable<IOperator> GetNextOperators()
        {
            if (_nextOperator != null)
                yield return _nextOperator;
        }
    }
}
