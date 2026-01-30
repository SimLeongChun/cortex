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
    /// Performs a left join between incoming stream elements (left side) and a state-backed table (right side) based on a shared key.
    /// Unlike an inner join, this operator emits a result for every left element, even if no matching right element is found.
    /// When no match exists, the join function receives <c>default(TRight)</c> for the right element.
    /// </summary>
    /// <typeparam name="TLeft">Type of the left stream elements.</typeparam>
    /// <typeparam name="TRight">Type of the right table elements stored in the <see cref="IDataStore{TKey, TRight}"/>.</typeparam>
    /// <typeparam name="TKey">Type of the key used for joining left elements with right elements.</typeparam>
    /// <typeparam name="TResult">Type of the result produced by the join operation.</typeparam>
    /// <remarks>
    /// <para>
    /// The left join guarantees that every element from the left stream will produce a result,
    /// making it suitable for scenarios where enrichment data may be optional or incomplete.
    /// </para>
    /// <para>
    /// Example use cases:
    /// <list type="bullet">
    ///   <item>Enriching order events with customer data that may not always exist</item>
    ///   <item>Adding optional metadata from a lookup table</item>
    ///   <item>Processing events where reference data may be delayed or missing</item>
    /// </list>
    /// </para>
    /// </remarks>
    public class StreamTableLeftJoinOperator<TLeft, TRight, TKey, TResult> : IOperator, IStatefulOperator, ITelemetryEnabled, IErrorHandlingEnabled
    {
        private readonly Func<TLeft, TKey> _keySelector;
        private readonly Func<TLeft, TRight, TResult> _joinFunction;
        private readonly IDataStore<TKey, TRight> _rightStateStore;
        private IOperator _nextOperator;

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private ICounter _matchedCounter;
        private ICounter _unmatchedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action _incrementMatchedCounter;
        private Action _incrementUnmatchedCounter;
        private Action<double> _recordProcessingTime;

        // Global error handling
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        /// <summary>
        /// Creates a new instance of <see cref="StreamTableLeftJoinOperator{TLeft, TRight, TKey, TResult}"/>.
        /// </summary>
        /// <param name="keySelector">A function that extracts a join key from a left stream element.</param>
        /// <param name="joinFunction">
        /// A function that combines a left stream element with a right element (or <c>default(TRight)</c> if no match) 
        /// to produce a <typeparamref name="TResult"/>.
        /// </param>
        /// <param name="rightStateStore">The state store that maps <typeparamref name="TKey"/> to right elements of type <typeparamref name="TRight"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown if any of the arguments are null.</exception>
        public StreamTableLeftJoinOperator(
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
                _processedCounter = metricsProvider.CreateCounter(
                    $"stream_table_left_join_processed_{typeof(TLeft).Name}",
                    "Number of items processed by StreamTableLeftJoinOperator");
                _matchedCounter = metricsProvider.CreateCounter(
                    $"stream_table_left_join_matched_{typeof(TLeft).Name}",
                    "Number of items that found a matching right element");
                _unmatchedCounter = metricsProvider.CreateCounter(
                    $"stream_table_left_join_unmatched_{typeof(TLeft).Name}",
                    "Number of items that did not find a matching right element");
                _processingTimeHistogram = metricsProvider.CreateHistogram(
                    $"stream_table_left_join_processing_time_{typeof(TLeft).Name}",
                    "Processing time for StreamTableLeftJoinOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"StreamTableLeftJoinOperator_{typeof(TLeft).Name}");

                _incrementProcessedCounter = () => _processedCounter.Increment();
                _incrementMatchedCounter = () => _matchedCounter.Increment();
                _incrementUnmatchedCounter = () => _unmatchedCounter.Increment();
                _recordProcessingTime = value => _processingTimeHistogram.Record(value);
            }
            else
            {
                _incrementProcessedCounter = null;
                _incrementMatchedCounter = null;
                _incrementUnmatchedCounter = null;
                _recordProcessingTime = null;
            }

            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled)
            {
                nextTelemetryEnabled.SetTelemetryProvider(telemetryProvider);
            }
        }

        /// <summary>
        /// Sets the error handling options for this operator and propagates them to downstream operators.
        /// </summary>
        /// <param name="options">The stream execution options containing error handling configuration.</param>
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
        /// The join function is always invoked - with the matching right element if found,
        /// or with <c>default(TRight)</c> if no match exists.
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
                $"StreamTableLeftJoinOperator<{typeof(TLeft).Name},{typeof(TRight).Name},{typeof(TKey).Name},{typeof(TResult).Name}>";

            bool executedSuccessfully;

            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();

                using (var span = _tracer.StartSpan("StreamTableLeftJoinOperator.Process"))
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

            // If executedSuccessfully == false ? global handler decided to Skip this left element
        }

        /// <summary>
        /// Performs the actual lookup on the right-side <see cref="IDataStore{TKey, TRight}"/>
        /// and applies the join function to produce a result for the next operator.
        /// Unlike an inner join, this always produces a result - using <c>default(TRight)</c> when no match is found.
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

            // Track match/unmatch metrics
            if (hasValue)
            {
                _incrementMatchedCounter?.Invoke();
            }
            else
            {
                _incrementUnmatchedCounter?.Invoke();
            }

            // Left join always emits - with matched value or default
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

            // Error handling ? downstream
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
