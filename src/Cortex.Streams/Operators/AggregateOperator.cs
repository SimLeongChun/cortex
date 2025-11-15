using Cortex.States;
using Cortex.States.Operators;
using Cortex.Streams.ErrorHandling;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace Cortex.Streams.Operators
{
    public class AggregateOperator<TKey, TCurrent, TAggregate> : IOperator, IStatefulOperator, ITelemetryEnabled, IErrorHandlingEnabled
    {
        private readonly Func<TCurrent, TKey> _keySelector;
        private readonly Func<TAggregate, TCurrent, TAggregate> _aggregateFunction;
        private readonly IDataStore<TKey, TAggregate> _stateStore;
        private IOperator _nextOperator;

        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

        public AggregateOperator(Func<TCurrent, TKey> keySelector, Func<TAggregate, TCurrent, TAggregate> aggregateFunction, IDataStore<TKey, TAggregate> stateStore)
        {
            _keySelector = keySelector;
            _aggregateFunction = aggregateFunction;
            _stateStore = stateStore;
        }

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _processedCounter = metricsProvider.CreateCounter($"aggregate_operator_processed_{typeof(TCurrent).Name}", "Number of items processed by AggregateOperator");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"aggregate_operator_processing_time_{typeof(TCurrent).Name}", "Processing time for AggregateOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"AggregateOperator_{typeof(TCurrent).Name}");

                // Cache delegates
                _incrementProcessedCounter = () => _processedCounter.Increment();
                _recordProcessingTime = value => _processingTimeHistogram.Record(value);
            }
            else
            {
                _incrementProcessedCounter = null;
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
            TCurrent typedInput;
            try
            {
                typedInput = (TCurrent)input;
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException(
                    $"Expected input of type {typeof(TCurrent).Name}, but received {input?.GetType().Name ?? "null"}");
            }

            var operatorName =
                $"AggregateOperator<{typeof(TKey).Name},{typeof(TCurrent).Name},{typeof(TAggregate).Name}>";

            bool executedSuccessfully;
            KeyValuePair<TKey, TAggregate> result = default;

            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();

                using (var span = _tracer.StartSpan("AggregateOperator.Process"))
                {
                    try
                    {
                        executedSuccessfully = ErrorHandlingHelper.TryExecute<TCurrent, KeyValuePair<TKey, TAggregate>>(
                            _executionOptions,
                            operatorName,
                            input,
                            current =>
                            {
                                var key = _keySelector(current);
                                TAggregate aggregate;

                                lock (_stateStore)
                                {
                                    aggregate = _stateStore.Get(key);
                                    aggregate = _aggregateFunction(aggregate, current);
                                    _stateStore.Put(key, aggregate);
                                }

                                return new KeyValuePair<TKey, TAggregate>(key, aggregate);
                            },
                            typedInput,
                            out result);

                        if (executedSuccessfully)
                        {
                            span.SetAttribute("key", result.Key?.ToString());
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
                executedSuccessfully = ErrorHandlingHelper.TryExecute<TCurrent, KeyValuePair<TKey, TAggregate>>(
                    _executionOptions,
                    operatorName,
                    input,
                    current =>
                    {
                        var key = _keySelector(current);
                        TAggregate aggregate;

                        lock (_stateStore)
                        {
                            aggregate = _stateStore.Get(key);
                            aggregate = _aggregateFunction(aggregate, current);
                            _stateStore.Put(key, aggregate);
                        }

                        return new KeyValuePair<TKey, TAggregate>(key, aggregate);
                    },
                    typedInput,
                    out result);
            }

            // On Skip (executedSuccessfully == false) => do not push downstream
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
