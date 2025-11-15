using Cortex.States;
using Cortex.States.Operators;
using Cortex.Streams.ErrorHandling;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cortex.Streams.Operators
{
    public class GroupByKeySilentlyOperator<TInput, TKey> : IOperator, IStatefulOperator, ITelemetryEnabled, IErrorHandlingEnabled
    {
        private readonly Func<TInput, TKey> _keySelector;
        private readonly IDataStore<TKey, List<TInput>> _stateStore;
        private IOperator _nextOperator;

        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;


        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

        public GroupByKeySilentlyOperator(Func<TInput, TKey> keySelector, IDataStore<TKey, List<TInput>> stateStore)
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
                    $"Expected input of type {typeof(TInput).Name}, but received {input?.GetType().Name ?? "null"}");
            }

            var operatorName =
                $"GroupByKeySilentlyOperator<{typeof(TInput).Name},{typeof(TKey).Name}>";

            bool executedSuccessfully;
            TKey key = default;
            List<TInput> group = null;

            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();

                // Keep original span name if you want strict backward compatibility:
                using (var span = _tracer.StartSpan("GroupByKeyOperator.Process"))
                {
                    try
                    {
                        executedSuccessfully =
                            ErrorHandlingHelper.TryExecute<TInput, List<TInput>>(
                                _executionOptions,
                                operatorName,
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

                                    return group;
                                },
                                typedInput,
                                out _);

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
                    ErrorHandlingHelper.TryExecute<TInput, List<TInput>>(
                        _executionOptions,
                        operatorName,
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

                            return localGroup;
                        },
                        typedInput,
                        out _);
            }

            // If error handler decided to Skip → do not forward to downstream operators
            if (!executedSuccessfully)
                return;

            // Continue processing with original element
            _nextOperator?.Process(input);
        }

        public void SetNext(IOperator nextOperator)
        {
            _nextOperator = nextOperator;

            // Propagate telemetry
            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled && _telemetryProvider != null)
            {
                nextTelemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }

            // propagate error handling
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
