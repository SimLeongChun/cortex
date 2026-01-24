using Cortex.States;
using Cortex.States.Operators;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Cortex.Streams.Operators.Windows
{
    /// <summary>
    /// A window operator that groups items into fixed-size, non-overlapping time windows.
    /// Each window has a fixed duration, and windows do not overlap.
    /// </summary>
    /// <typeparam name="TInput">The type of the input items.</typeparam>
    /// <typeparam name="TKey">The type of the key used to partition windows.</typeparam>
    public class TumblingWindowOperator<TInput, TKey> : IOperator, IStatefulOperator, ITelemetryEnabled, IDisposable
    {
        private readonly Func<TInput, TKey> _keySelector;
        private readonly Func<TInput, DateTime> _timestampSelector;
        private readonly TimeSpan _windowSize;
        private readonly IDataStore<string, List<TInput>> _stateStore;
        private readonly Dictionary<string, DateTime> _windowEndTimes;
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
        /// Initializes a new instance of the <see cref="TumblingWindowOperator{TInput, TKey}"/> class.
        /// </summary>
        /// <param name="keySelector">A function to extract the key from each input item.</param>
        /// <param name="timestampSelector">A function to extract the timestamp from each input item.</param>
        /// <param name="windowSize">The size of each tumbling window.</param>
        /// <param name="stateStore">The state store to use for storing window data.</param>
        public TumblingWindowOperator(
            Func<TInput, TKey> keySelector,
            Func<TInput, DateTime> timestampSelector,
            TimeSpan windowSize,
            IDataStore<string, List<TInput>> stateStore)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _timestampSelector = timestampSelector ?? throw new ArgumentNullException(nameof(timestampSelector));
            _windowSize = windowSize;
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _windowEndTimes = new Dictionary<string, DateTime>();

            // Start window evaluation timer
            _windowTimer = new Timer(EvaluateWindows, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        }

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _processedCounter = metricsProvider.CreateCounter($"tumbling_window_operator_processed_{typeof(TInput).Name}", "Number of items processed by TumblingWindowOperator");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"tumbling_window_operator_processing_time_{typeof(TInput).Name}", "Processing time for TumblingWindowOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"TumblingWindowOperator_{typeof(TInput).Name}");

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
                using (var span = _tracer.StartSpan("TumblingWindowOperator.Process"))
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

            lock (_lock)
            {
                var windowItems = _stateStore.Get(windowKey) ?? new List<TInput>();
                windowItems.Add(typedInput);
                _stateStore.Put(windowKey, windowItems);

                if (!_windowEndTimes.ContainsKey(windowKey))
                {
                    _windowEndTimes[windowKey] = windowEnd;
                }
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
            List<string> expiredWindows = new List<string>();

            lock (_lock)
            {
                foreach (var kvp in _windowEndTimes)
                {
                    if (now >= kvp.Value)
                    {
                        expiredWindows.Add(kvp.Key);
                    }
                }

                foreach (var windowKey in expiredWindows)
                {
                    var windowItems = _stateStore.Get(windowKey);
                    if (windowItems != null && windowItems.Count > 0)
                    {
                        var windowEnd = _windowEndTimes[windowKey];
                        var windowStart = windowEnd - _windowSize;

                        // Parse the key from the window key
                        var keyEndIndex = windowKey.LastIndexOf('_');
                        var keyString = windowKey.Substring(0, keyEndIndex);

                        // Create window result
                        var windowResult = new WindowResult<string, TInput>(
                            keyString,
                            windowStart,
                            windowEnd,
                            windowItems);

                        // Emit the window result
                        _nextOperator?.Process(windowResult);

                        // Clean up
                        _stateStore.Remove(windowKey);
                    }

                    _windowEndTimes.Remove(windowKey);
                }
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
