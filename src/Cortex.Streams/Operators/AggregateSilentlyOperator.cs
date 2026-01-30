using Cortex.States;
using Cortex.States.Operators;
using Cortex.Streams.ErrorHandling;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cortex.Streams.Operators
{
    public class AggregateSilentlyOperator<TKey, TInput, TAggregate> : IOperator, IStatefulOperator, ITelemetryEnabled, IErrorHandlingEnabled
    {
        private readonly Func<TInput, TKey> _keySelector;
        private readonly Func<TAggregate, TInput, TAggregate> _aggregateFunction;
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

        public AggregateSilentlyOperator(Func<TInput, TKey> keySelector, Func<TAggregate, TInput, TAggregate> aggregateFunction, IDataStore<TKey, TAggregate> stateStore)
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
                _processedCounter = metricsProvider.CreateCounter($"aggregate_operator_processed_{typeof(TInput).Name}", "Number of items processed by AggregateOperator");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"aggregate_operator_processing_time_{typeof(TInput).Name}", "Processing time for AggregateOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"AggregateOperator_{typeof(TInput).Name}");

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
                $"AggregateSilentlyOperator<{typeof(TKey).Name},{typeof(TInput).Name},{typeof(TAggregate).Name}>";

            bool executedSuccessfully;
            TKey key = default;
            TAggregate aggregate = default;

            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();

                using (var span = _tracer.StartSpan("AggregateSilentlyOperator.Process"))
                {
                    try
                    {
                        executedSuccessfully = ErrorHandlingHelper.TryExecute<TInput, TAggregate>(
                            _executionOptions,
                            operatorName,
                            input,
                            current =>
                            {
                                key = _keySelector(current);
                                lock (_stateStore)
                                {
                                    aggregate = _stateStore.Get(key);
                                    aggregate = _aggregateFunction(aggregate, current);
                                    _stateStore.Put(key, aggregate);
                                }
                                return aggregate;
                            },
                            typedInput,
                            out _);

                        if (executedSuccessfully)
                        {
                            span.SetAttribute("key", key?.ToString());
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
                executedSuccessfully = ErrorHandlingHelper.TryExecute<TInput, TAggregate>(
                    _executionOptions,
                    operatorName,
                    input,
                    current =>
                    {
                        key = _keySelector(current);
                        lock (_stateStore)
                        {
                            aggregate = _stateStore.Get(key);
                            aggregate = _aggregateFunction(aggregate, current);
                            _stateStore.Put(key, aggregate);
                        }
                        return aggregate;
                    },
                    typedInput,
                    out _);
            }

            // If the error handling decided to Skip, do not forward the element.
            if (!executedSuccessfully)
                return;

            // Continue normal processing with original input
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
