using Cortex.States;
using Cortex.States.Operators;
using Cortex.Streams.Operators.Windows.Triggers;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Cortex.Streams.Operators.Windows
{
    /// <summary>
    /// An advanced tumbling window operator with support for custom triggers and state modes.
    /// </summary>
    /// <typeparam name="TInput">The type of the input items.</typeparam>
    /// <typeparam name="TKey">The type of the key used to partition windows.</typeparam>
    public class AdvancedTumblingWindowOperator<TInput, TKey> : IOperator, IStatefulOperator, ITelemetryEnabled, IDisposable
    {
        private readonly Func<TInput, TKey> _keySelector;
        private readonly Func<TInput, DateTime> _timestampSelector;
        private readonly TimeSpan _windowSize;
        private readonly IDataStore<string, List<TInput>> _stateStore;
        private readonly WindowConfiguration<TInput> _config;
        private readonly Dictionary<string, WindowState> _windowStates;
        private readonly object _lock = new object();
        private IOperator _nextOperator;
        private Timer _windowTimer;
        private bool _disposed;

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

        /// <summary>
        /// Internal window state tracking.
        /// </summary>
        private class WindowState
        {
            public DateTime WindowStart { get; set; }
            public DateTime WindowEnd { get; set; }
            public TriggerContext<TInput> TriggerContext { get; set; }
            public int EmissionSequence { get; set; }
            public List<TInput> LastEmittedItems { get; set; }
            public bool HasFired { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AdvancedTumblingWindowOperator{TInput, TKey}"/> class.
        /// </summary>
        /// <param name="keySelector">A function to extract the key from each input item.</param>
        /// <param name="timestampSelector">A function to extract the timestamp from each input item.</param>
        /// <param name="windowSize">The size of each tumbling window.</param>
        /// <param name="stateStore">The state store to use for storing window data.</param>
        /// <param name="config">The window configuration.</param>
        public AdvancedTumblingWindowOperator(
            Func<TInput, TKey> keySelector,
            Func<TInput, DateTime> timestampSelector,
            TimeSpan windowSize,
            IDataStore<string, List<TInput>> stateStore,
            WindowConfiguration<TInput> config = null)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _timestampSelector = timestampSelector ?? throw new ArgumentNullException(nameof(timestampSelector));
            _windowSize = windowSize;
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _config = config ?? new WindowConfiguration<TInput>();
            _windowStates = new Dictionary<string, WindowState>();

            // Start window evaluation timer
            _windowTimer = new Timer(EvaluateWindows, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        }

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _processedCounter = metricsProvider.CreateCounter($"advanced_tumbling_window_operator_processed_{typeof(TInput).Name}", "Number of items processed by AdvancedTumblingWindowOperator");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"advanced_tumbling_window_operator_processing_time_{typeof(TInput).Name}", "Processing time for AdvancedTumblingWindowOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"AdvancedTumblingWindowOperator_{typeof(TInput).Name}");

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

        public void Process(object input)
        {
            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();
                using (var span = _tracer.StartSpan("AdvancedTumblingWindowOperator.Process"))
                {
                    try
                    {
                        ProcessInternal(input);
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
                ProcessInternal(input);
            }
        }

        private void ProcessInternal(object input)
        {
            var typedInput = (TInput)input;
            var key = _keySelector(typedInput);
            var timestamp = _timestampSelector(typedInput);

            // Calculate window boundaries
            var windowStart = GetWindowStart(timestamp);
            var windowEnd = windowStart + _windowSize;
            var windowKey = GetWindowKey(key, windowStart);
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                // Check for late data
                if (now > windowEnd + _config.AllowedLateness)
                {
                    _config.OnLateEvent?.Invoke(typedInput, timestamp);
                    return;
                }

                // Get or create window state
                if (!_windowStates.TryGetValue(windowKey, out var windowState))
                {
                    windowState = new WindowState
                    {
                        WindowStart = windowStart,
                        WindowEnd = windowEnd,
                        TriggerContext = new TriggerContext<TInput>(windowKey, () => _stateStore.Get(windowKey)?.Count ?? 0),
                        EmissionSequence = 0,
                        LastEmittedItems = new List<TInput>()
                    };
                    _windowStates[windowKey] = windowState;
                }

                // Add item to window
                var windowItems = _stateStore.Get(windowKey) ?? new List<TInput>();
                windowItems.Add(typedInput);
                _stateStore.Put(windowKey, windowItems);

                // Evaluate trigger on element
                var triggerResult = _config.Trigger.OnElement(typedInput, timestamp, windowStart, windowEnd, windowState.TriggerContext);
                HandleTriggerResult(triggerResult, windowKey, windowState, key.ToString(), isOnTime: false);
            }
        }

        private DateTime GetWindowStart(DateTime timestamp)
        {
            var ticks = timestamp.Ticks;
            var windowTicks = _windowSize.Ticks;
            var windowStartTicks = (ticks / windowTicks) * windowTicks;
            return new DateTime(windowStartTicks, timestamp.Kind);
        }

        private string GetWindowKey(TKey key, DateTime windowStart)
        {
            return $"{key}_{windowStart.Ticks}";
        }

        private void EvaluateWindows(object state)
        {
            var now = DateTime.UtcNow;
            List<string> windowsToRemove = new List<string>();

            lock (_lock)
            {
                foreach (var kvp in _windowStates.ToList())
                {
                    var windowKey = kvp.Key;
                    var windowState = kvp.Value;

                    // Evaluate processing time trigger
                    var triggerResult = _config.Trigger.OnProcessingTime(now, windowState.WindowStart, windowState.WindowEnd, windowState.TriggerContext);

                    // Parse the key from the window key
                    var keyEndIndex = windowKey.LastIndexOf('_');
                    var keyString = windowKey.Substring(0, keyEndIndex);

                    var isOnTime = now >= windowState.WindowEnd;
                    HandleTriggerResult(triggerResult, windowKey, windowState, keyString, isOnTime);

                    // Remove window if it's past allowed lateness
                    if (now > windowState.WindowEnd + _config.AllowedLateness && windowState.HasFired)
                    {
                        windowsToRemove.Add(windowKey);
                    }
                }

                // Clean up old windows
                foreach (var windowKey in windowsToRemove)
                {
                    _windowStates.Remove(windowKey);
                    _config.Trigger.Clear(_windowStates.ContainsKey(windowKey) ? _windowStates[windowKey].WindowStart : DateTime.MinValue,
                        _windowStates.ContainsKey(windowKey) ? _windowStates[windowKey].WindowEnd : DateTime.MinValue,
                        null);
                }
            }
        }

        private void HandleTriggerResult(TriggerResult result, string windowKey, WindowState windowState, string keyString, bool isOnTime)
        {
            if (result == TriggerResult.Continue)
            {
                return;
            }

            var windowItems = _stateStore.Get(windowKey);
            if (windowItems == null || windowItems.Count == 0)
            {
                return;
            }

            windowState.EmissionSequence++;
            var isFinal = result == TriggerResult.FireAndPurge;
            var emissionType = isFinal ? (isOnTime ? WindowEmissionType.OnTime : WindowEmissionType.Late) : WindowEmissionType.Early;

            // Handle state mode
            List<TInput> itemsToEmit;
            switch (_config.StateMode)
            {
                case WindowStateMode.Accumulating:
                    itemsToEmit = new List<TInput>(windowItems);
                    break;

                case WindowStateMode.AccumulatingAndRetracting:
                    // First emit retraction for previous result if there was one
                    if (windowState.LastEmittedItems.Count > 0)
                    {
                        var retractionResult = new WindowResult<string, TInput>(
                            keyString,
                            windowState.WindowStart,
                            windowState.WindowEnd,
                            windowState.LastEmittedItems,
                            WindowEmissionType.Retraction,
                            false,
                            DateTime.UtcNow,
                            windowState.EmissionSequence - 1);
                        _nextOperator?.Process(retractionResult);
                    }
                    itemsToEmit = new List<TInput>(windowItems);
                    windowState.LastEmittedItems = new List<TInput>(itemsToEmit);
                    break;

                case WindowStateMode.Discarding:
                default:
                    // Only emit items since last emission
                    if (windowState.LastEmittedItems.Count > 0)
                    {
                        itemsToEmit = windowItems.Skip(windowState.LastEmittedItems.Count).ToList();
                    }
                    else
                    {
                        itemsToEmit = new List<TInput>(windowItems);
                    }
                    windowState.LastEmittedItems = new List<TInput>(windowItems);
                    break;
            }

            if (itemsToEmit.Count > 0)
            {
                var windowResult = new WindowResult<string, TInput>(
                    keyString,
                    windowState.WindowStart,
                    windowState.WindowEnd,
                    itemsToEmit,
                    emissionType,
                    isFinal,
                    DateTime.UtcNow,
                    windowState.EmissionSequence);

                _nextOperator?.Process(windowResult);
            }

            windowState.HasFired = true;

            // Purge state if required
            if (result == TriggerResult.FireAndPurge)
            {
                _stateStore.Remove(windowKey);
                _windowStates.Remove(windowKey);
            }
        }

        public void SetNext(IOperator nextOperator)
        {
            _nextOperator = nextOperator;

            // Propagate telemetry
            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled && _telemetryProvider != null)
            {
                nextTelemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }
        }

        public IEnumerable<IDataStore> GetStateStores()
        {
            yield return _stateStore;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _windowTimer?.Dispose();
                    _windowTimer = null;
                }
                _disposed = true;
            }
        }
    }
}
