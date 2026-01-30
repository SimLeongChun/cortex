using Cortex.States;
using Cortex.States.Operators;
using Cortex.Streams.ErrorHandling;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cortex.Streams.Operators
{
    public class GroupByKeyOperator<TInput, TKey> : IOperator, IStatefulOperator, ITelemetryEnabled, IErrorHandlingEnabled
    {
        private readonly Func<TInput, TKey> _keySelector;
        private readonly IDataStore<TKey, List<TInput>> _stateStore;
        private IOperator _nextOperator;

        // Cached operator name to avoid string allocation on hot path
        private static readonly string OperatorName = $"GroupByKeyOperator<{typeof(TInput).Name},{typeof(TKey).Name}>";
        private static readonly string InputTypeName = typeof(TInput).Name;

        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

        public GroupByKeyOperator(Func<TInput, TKey> keySelector, IDataStore<TKey, List<TInput>> stateStore)
        {
            _keySelector = keySelector;
            _stateStore = stateStore;
        }

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _processedCounter = metricsProvider.CreateCounter($"groupby_operator_processed_{typeof(TInput).Name}", "Number of items processed by GroupByKeyOperator");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"groupby_operator_processing_time_{typeof(TInput).Name}", "Processing time for GroupByKeyOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"GroupByKeyOperator_{typeof(TInput).Name}");

                // Cache delegates
                _incrementProcessedCounter = () => _processedCounter.Increment();
                _recordProcessingTime = value => _processingTimeHistogram.Record(value);
            }
            else
            {
                _incrementProcessedCounter = null;
                _recordProcessingTime = null;
            }

            // Propagate telemetry
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
            TKey key = default;
            List<TInput> group = null;
            KeyValuePair<TKey, List<TInput>> result = default;

            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();

                using (var span = _tracer.StartSpan("GroupByKeyOperator.Process"))
                {
                    try
                    {
                        executedSuccessfully =
                            ErrorHandlingHelper.TryExecute<TInput, KeyValuePair<TKey, List<TInput>>>(
                                _executionOptions,
                                OperatorName,
                                input,
                                current =>
                                {
                                    key = _keySelector(current);

                                    lock (_stateStore)
                                    {
                                        group = _stateStore.Get(key) ?? new List<TInput>();
                                        group.Add(current);
                                        _stateStore.Put(key, group);
                                    }

                                    return new KeyValuePair<TKey, List<TInput>>(key, group);
                                },
                                typedInput,
                                out result);

                        if (executedSuccessfully)
                        {
                            span.SetAttribute("key", key?.ToString());
                            span.SetAttribute("group_size", group?.Count.ToString());
                        }

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
                executedSuccessfully =
                    ErrorHandlingHelper.TryExecute<TInput, KeyValuePair<TKey, List<TInput>>>(
                        _executionOptions,
                        OperatorName,
                        input,
                        current =>
                        {
                            var localKey = _keySelector(current);
                            List<TInput> localGroup;

                            lock (_stateStore)
                            {
                                localGroup = _stateStore.Get(localKey) ?? new List<TInput>();
                                localGroup.Add(current);
                                _stateStore.Put(localKey, localGroup);
                            }

                            return new KeyValuePair<TKey, List<TInput>>(localKey, localGroup);
                        },
                        typedInput,
                        out result);
            }

            // If handler decided to Skip → do not pass downstream
            if (!executedSuccessfully)
                return;

            _nextOperator?.Process(result);
        }

        public void SetNext(IOperator nextOperator)
        {
            _nextOperator = nextOperator;

            // Propagate telemetry
            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled && _telemetryProvider != null)
            {
                nextTelemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }

            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }
        }

        public IEnumerable<IDataStore> GetStateStores()
        {
            yield return _stateStore;
        }
    }
}
