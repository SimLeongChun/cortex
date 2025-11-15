using Cortex.States;
using Cortex.States.Operators;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Windows;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Cortex.Streams.Operators
{
    /// <summary>
    /// An operator that performs tumbling window aggregation.
    /// </summary>
    /// <typeparam name="TInput">The type of input data.</typeparam>
    /// <typeparam name="TKey">The type of the key to group by.</typeparam>
    /// <typeparam name="TWindowOutput">The type of the output after windowing.</typeparam>
    public class TumblingWindowOperator<TInput, TKey, TWindowOutput> :
        IOperator,
        IStatefulOperator,
        ITelemetryEnabled,
        IErrorHandlingEnabled
    {
        private readonly Func<TInput, TKey> _keySelector;
        private readonly TimeSpan _windowDuration;
        private readonly Func<IEnumerable<TInput>, TWindowOutput> _windowFunction;
        private readonly IDataStore<TKey, WindowState<TInput>> _windowStateStore;
        private readonly IDataStore<WindowKey<TKey>, TWindowOutput> _windowResultsStateStore;
        private IOperator _nextOperator;

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

        // Timer + locking for window expiration
        private readonly Timer _windowExpirationTimer;
        private readonly object _stateLock = new object();

        // Global error handling
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        public TumblingWindowOperator(
            Func<TInput, TKey> keySelector,
            TimeSpan windowDuration,
            Func<IEnumerable<TInput>, TWindowOutput> windowFunction,
            IDataStore<TKey, WindowState<TInput>> windowStateStore,
            IDataStore<WindowKey<TKey>, TWindowOutput> windowResultsStateStore = null)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _windowDuration = windowDuration;
            _windowFunction = windowFunction ?? throw new ArgumentNullException(nameof(windowFunction));
            _windowStateStore = windowStateStore ?? throw new ArgumentNullException(nameof(windowStateStore));
            _windowResultsStateStore = windowResultsStateStore;

            // Global timer: every windowDuration, check for expired windows.
            _windowExpirationTimer = new Timer(WindowExpirationCallback, null, _windowDuration, _windowDuration);
        }

        #region Telemetry

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();

                _processedCounter = metricsProvider.CreateCounter(
                    $"TumblingWindowOperator_Processed_{typeof(TInput).Name}",
                    "Number of items processed by TumblingWindowOperator");

                _processingTimeHistogram = metricsProvider.CreateHistogram(
                    $"TumblingWindowOperator_ProcessingTime_{typeof(TInput).Name}",
                    "Processing time for TumblingWindowOperator");

                _tracer = _telemetryProvider
                    .GetTracingProvider()
                    .GetTracer($"TumblingWindowOperator_{typeof(TInput).Name}");

                _incrementProcessedCounter = () => _processedCounter.Increment();
                _recordProcessingTime = value => _processingTimeHistogram.Record(value);
            }
            else
            {
                _incrementProcessedCounter = null;
                _recordProcessingTime = null;
            }

            // Propagate telemetry to next operator, if any
            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled)
            {
                nextTelemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }
        }

        #endregion

        #region Error handling

        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;

            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }
        }

        #endregion

        #region IOperator

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
                    $"Expected input of type {typeof(TInput).Name}, but received {input?.GetType().Name ?? "null"}");
            }

            var operatorName =
                $"TumblingWindowOperator<{typeof(TInput).Name},{typeof(TKey).Name},{typeof(TWindowOutput).Name}>";

            bool executedSuccessfully;

            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();

                using (var span = _tracer.StartSpan("TumblingWindowOperator.Process"))
                {
                    try
                    {
                        executedSuccessfully = ErrorHandlingHelper.TryExecute<TInput>(
                            _executionOptions,
                            operatorName,
                            input,
                            () =>
                            {
                                ProcessInput(typedInput);
                                // dummy return
                                return typedInput;
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
                executedSuccessfully = ErrorHandlingHelper.TryExecute<TInput>(
                    _executionOptions,
                    operatorName,
                    input,
                    () =>
                    {
                        ProcessInput(typedInput);
                        return typedInput;
                    });
            }

            // If executedSuccessfully == false -> error handler decided to Skip this element.
            // No further action here; windows were not updated for this bad event.
        }

        #endregion

        #region Window logic

        private void ProcessInput(TInput input)
        {
            var key = _keySelector(input);
            var currentTime = DateTime.UtcNow;

            WindowState<TInput> windowState;
            bool isNewWindow = false;

            lock (_stateLock)
            {
                if (!_windowStateStore.ContainsKey(key))
                {
                    // Initialize window state
                    var windowStartTime = GetWindowStartTime(currentTime);
                    windowState = new WindowState<TInput>
                    {
                        WindowStartTime = windowStartTime,
                        Events = new List<TInput>()
                    };
                    _windowStateStore.Put(key, windowState);
                    isNewWindow = true;
                }
                else
                {
                    windowState = _windowStateStore.Get(key);
                }

                // Check if the event falls into the current window
                if (currentTime >= windowState.WindowStartTime &&
                    currentTime < windowState.WindowStartTime + _windowDuration)
                {
                    // Event falls into current window
                    windowState.Events.Add(input);
                    _windowStateStore.Put(key, windowState);
                }
                else
                {
                    // Window has closed, process the window
                    ProcessWindow(key, windowState);

                    // Start a new window
                    var newWindowStartTime = GetWindowStartTime(currentTime);
                    windowState = new WindowState<TInput>
                    {
                        WindowStartTime = newWindowStartTime,
                        Events = new List<TInput> { input }
                    };
                    _windowStateStore.Put(key, windowState);
                    isNewWindow = true;
                }
            }

            if (isNewWindow)
            {
                // We rely on global timer; no per-key timers needed.
            }
        }

        private void ProcessWindow(TKey key, WindowState<TInput> windowState)
        {
            // This is user code: windowFunction can throw -> handled via caller’s ErrorHandlingHelper context
            var windowOutput = _windowFunction(windowState.Events);

            // Optionally store the window result
            if (_windowResultsStateStore != null)
            {
                var resultKey = new WindowKey<TKey>
                {
                    Key = key,
                    WindowStartTime = windowState.WindowStartTime
                };

                _windowResultsStateStore.Put(resultKey, windowOutput);
            }

            // Emit the window output downstream
            _nextOperator?.Process(windowOutput);

            // Remove the window state
            _windowStateStore.Remove(key);
        }

        private void WindowExpirationCallback(object state)
        {
            var operatorName =
                $"TumblingWindowOperator<{typeof(TInput).Name},{typeof(TKey).Name},{typeof(TWindowOutput).Name}>.Timer";

            // We don't need the return value here; we just want consistent error handling behavior.
            ErrorHandlingHelper.TryExecute<object>(
                _executionOptions,
                operatorName,
                state,
                () =>
                {
                    var currentTime = DateTime.UtcNow;
                    var keysToProcess = new List<TKey>();

                    lock (_stateLock)
                    {
                        var allKeys = _windowStateStore.GetKeys();

                        foreach (var key in allKeys)
                        {
                            var windowState = _windowStateStore.Get(key);
                            if (windowState != null &&
                                currentTime >= windowState.WindowStartTime + _windowDuration)
                            {
                                // Window has expired
                                keysToProcess.Add(key);
                            }
                        }
                    }

                    // Process expired windows outside the lock to avoid long lock durations
                    foreach (var key in keysToProcess)
                    {
                        WindowState<TInput> windowState;

                        lock (_stateLock)
                        {
                            windowState = _windowStateStore.Get(key);
                            if (windowState == null)
                                continue; // Already processed
                        }

                        // Any exception in ProcessWindow propagates back into ErrorHandlingHelper and goes through OnError/Strategy
                        ProcessWindow(key, windowState);
                    }

                    return null;
                });
        }

        private DateTime GetWindowStartTime(DateTime timestamp)
        {
            var windowStartTicks =
                (long)(timestamp.Ticks / _windowDuration.Ticks) * _windowDuration.Ticks;
            return new DateTime(windowStartTicks, DateTimeKind.Utc);
        }

        #endregion

        #region IStatefulOperator

        public IEnumerable<IDataStore> GetStateStores()
        {
            yield return _windowStateStore;

            if (_windowResultsStateStore != null)
                yield return _windowResultsStateStore;
        }

        #endregion

        #region Next operator wiring

        public void SetNext(IOperator nextOperator)
        {
            _nextOperator = nextOperator;

            // Propagate telemetry to downstream operators
            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled && _telemetryProvider != null)
            {
                nextTelemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }

            // Propagate error handling to downstream operators
            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }
        }

        #endregion
    }
}
