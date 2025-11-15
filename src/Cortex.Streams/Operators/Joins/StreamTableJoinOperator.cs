using Cortex.States;
using Cortex.States.Operators;
using Cortex.Streams.ErrorHandling;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cortex.Streams.Operators
{
    /// <summary>
    /// Joins incoming stream elements (left side) with a state-backed table (right side) based on a shared key.
    /// The join operation is performed on each incoming element using the provided <see cref="IDataStore{TKey, TRight}"/>.
    /// </summary>
    /// <typeparam name="TLeft">Type of the left stream elements.</typeparam>
    /// <typeparam name="TRight">Type of the right table elements stored in the <see cref="IDataStore{TKey, TRight}"/>.</typeparam>
    /// <typeparam name="TKey">Type of the key used for joining left elements with right elements.</typeparam>
    /// <typeparam name="TResult">Type of the result produced by the join operation.</typeparam>
    public class StreamTableJoinOperator<TLeft, TRight, TKey, TResult> : IOperator, IStatefulOperator, ITelemetryEnabled, IErrorHandlingEnabled
    {
        private readonly Func<TLeft, TKey> _keySelector;
        private readonly Func<TLeft, TRight, TResult> _joinFunction;
        private readonly IDataStore<TKey, TRight> _rightStateStore;
        private IOperator _nextOperator;

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

        // Global error handling
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;


        /// <summary>
        /// Creates a new instance of <see cref="StreamTableJoinOperator{TLeft, TRight, TKey, TResult}"/>.
        /// </summary>
        /// <param name="keySelector">A function that extracts a join key from a left stream element.</param>
        /// <param name="joinFunction">A function that combines a left stream element with a right element to produce a <typeparamref name="TResult"/>.</param>
        /// <param name="rightStateStore">The state store that maps <typeparamref name="TKey"/> to right elements of type <typeparamref name="TRight"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown if any of the arguments are null.</exception>
        public StreamTableJoinOperator(
            Func<TLeft, TKey> keySelector,
            Func<TLeft, TRight, TResult> joinFunction,
            IDataStore<TKey, TRight> rightStateStore)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _joinFunction = joinFunction ?? throw new ArgumentNullException(nameof(joinFunction));
            _rightStateStore = rightStateStore ?? throw new ArgumentNullException(nameof(rightStateStore));
        }


        /// <summary>
        /// Sets the telemetry provider which collects and reports metrics and tracing information.
        /// </summary>
        /// <param name="telemetryProvider">An implementation of <see cref="ITelemetryProvider"/>.</param>
        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _processedCounter = metricsProvider.CreateCounter($"stream_table_join_processed_{typeof(TLeft).Name}", "Number of items processed by StreamTableJoinOperator");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"stream_table_join_processing_time_{typeof(TLeft).Name}", "Processing time for StreamTableJoinOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"StreamTableJoinOperator_{typeof(TLeft).Name}");

                _incrementProcessedCounter = () => _processedCounter.Increment();
                _recordProcessingTime = value => _processingTimeHistogram.Record(value);
            }
            else
            {
                _incrementProcessedCounter = null;
                _recordProcessingTime = null;
            }

            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled)
            {
                nextTelemetryEnabled.SetTelemetryProvider(telemetryProvider);
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

        /// <summary>
        /// Processes an incoming item from the left stream.
        /// If the item key exists in the right-hand state store, the join function is invoked,
        /// and the result is pushed to the next operator.
        /// </summary>
        /// <param name="input">An input item of type <typeparamref name="TLeft"/> to be joined.</param>
        public void Process(object input)
        {
            // Only react to TLeft; ignore anything else (e.g., other branches reusing operator)
            TLeft left;
            try
            {
                left = (TLeft)input;
            }
            catch (InvalidCastException)
            {
                return;
            }

            var operatorName =
                $"StreamTableJoinOperator<{typeof(TLeft).Name},{typeof(TRight).Name},{typeof(TKey).Name},{typeof(TResult).Name}>";

            bool executedSuccessfully;

            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();

                using (var span = _tracer.StartSpan("StreamTableJoinOperator.Process"))
                {
                    try
                    {
                        executedSuccessfully = ErrorHandlingHelper.TryExecute<TLeft>(
                            _executionOptions,
                            operatorName,
                            input,
                            () =>
                            {
                                ProcessLeft(left);
                                return left; // dummy return for generic helper
                            });

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
                executedSuccessfully = ErrorHandlingHelper.TryExecute<TLeft>(
                    _executionOptions,
                    operatorName,
                    input,
                    () =>
                    {
                        ProcessLeft(left);
                        return left;
                    });
            }

            // If executedSuccessfully == false → global handler decided to Skip this left element
        }


        /// <summary>
        /// Performs the actual lookup on the right-side <see cref="IDataStore{TKey, TRight}"/>
        /// and, if found, applies the join function to produce a result for the next operator.
        /// </summary>
        /// <param name="left">The left input element to be joined.</param>
        private void ProcessLeft(TLeft left)
        {
            var key = _keySelector(left);
            TRight right = default;
            bool hasValue = false;

            lock (_rightStateStore)
            {
                if (_rightStateStore.ContainsKey(key))
                {
                    right = _rightStateStore.Get(key);
                    hasValue = true;
                }
            }

            if (!hasValue)
                return;

            var result = _joinFunction(left, right);
            _nextOperator?.Process(result);
        }

        /// <summary>
        /// Sets the next operator in the processing chain.
        /// The result of this operator's join operation is passed on to the next operator via <see cref="Process(object)"/>.
        /// </summary>
        /// <param name="nextOperator">The next operator to receive joined results.</param>
        public void SetNext(IOperator nextOperator)
        {
            _nextOperator = nextOperator;

            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled && _telemetryProvider != null)
            {
                nextTelemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }

            // Error handling → downstream
            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }
        }

        /// <summary>
        /// Retrieves all state stores that this operator uses internally.
        /// In this case, the operator only returns the right-side <see cref="IDataStore{TKey, TRight}"/>.
        /// </summary>
        /// <returns>An enumerable of the operator's state stores.</returns>
        public IEnumerable<IDataStore> GetStateStores()
        {
            yield return _rightStateStore;
        }
    }
}