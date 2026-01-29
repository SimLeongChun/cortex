using Cortex.States;
using Cortex.States.Operators;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators.Joins;
using Cortex.Telemetry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Cortex.Streams.Operators
{
    /// <summary>
    /// Performs a windowed join between two streams based on a shared key.
    /// Events from both streams are buffered within the configured time window,
    /// and matching is performed when events arrive from either side.
    /// </summary>
    /// <typeparam name="TLeft">Type of elements from the left stream.</typeparam>
    /// <typeparam name="TRight">Type of elements from the right stream.</typeparam>
    /// <typeparam name="TKey">Type of the key used for matching.</typeparam>
    /// <typeparam name="TResult">Type of the result produced by the join.</typeparam>
    public class StreamStreamJoinOperator<TLeft, TRight, TKey, TResult> : IOperator, IStatefulOperator, ITelemetryEnabled, IErrorHandlingEnabled, IDisposable
    {
        private readonly Func<TLeft, TKey> _leftKeySelector;
        private readonly Func<TRight, TKey> _rightKeySelector;
        private readonly Func<TLeft, TRight, TResult> _joinFunction;
        private readonly Func<TLeft, DateTime> _leftTimestampSelector;
        private readonly Func<TRight, DateTime> _rightTimestampSelector;
        private readonly StreamJoinConfiguration _configuration;

        // Buffers for windowed data - key -> list of (element, timestamp, matched flag)
        private readonly ConcurrentDictionary<TKey, List<BufferedElement<TLeft>>> _leftBuffer;
        private readonly ConcurrentDictionary<TKey, List<BufferedElement<TRight>>> _rightBuffer;

        // State stores for persistence (optional)
        private readonly IDataStore<TKey, List<BufferedElement<TLeft>>> _leftStateStore;
        private readonly IDataStore<TKey, List<BufferedElement<TRight>>> _rightStateStore;

        private IOperator _nextOperator;
        private readonly object _bufferLock = new object();

        // Cleanup timer
        private Timer _cleanupTimer;
        private bool _disposed;

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _leftProcessedCounter;
        private ICounter _rightProcessedCounter;
        private ICounter _matchedCounter;
        private ICounter _leftUnmatchedCounter;
        private ICounter _rightUnmatchedCounter;
        private ICounter _expiredCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;

        // Error handling
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        /// <summary>
        /// Represents an element buffered for windowed joining.
        /// </summary>
        public class BufferedElement<T>
        {
            public T Element { get; set; }
            public DateTime Timestamp { get; set; }
            public bool Matched { get; set; }
            public DateTime ExpiresAt { get; set; }

            public BufferedElement(T element, DateTime timestamp, TimeSpan windowSize)
            {
                Element = element;
                Timestamp = timestamp;
                Matched = false;
                ExpiresAt = timestamp.Add(windowSize);
            }
        }

        /// <summary>
        /// Creates a new stream-stream join operator.
        /// </summary>
        /// <param name="leftKeySelector">Function to extract the join key from left stream elements.</param>
        /// <param name="rightKeySelector">Function to extract the join key from right stream elements.</param>
        /// <param name="leftTimestampSelector">Function to extract the event timestamp from left stream elements.</param>
        /// <param name="rightTimestampSelector">Function to extract the event timestamp from right stream elements.</param>
        /// <param name="joinFunction">Function to combine matched left and right elements.</param>
        /// <param name="configuration">Join configuration including window size and join type.</param>
        /// <param name="leftStateStore">Optional state store for left buffer persistence.</param>
        /// <param name="rightStateStore">Optional state store for right buffer persistence.</param>
        public StreamStreamJoinOperator(
            Func<TLeft, TKey> leftKeySelector,
            Func<TRight, TKey> rightKeySelector,
            Func<TLeft, DateTime> leftTimestampSelector,
            Func<TRight, DateTime> rightTimestampSelector,
            Func<TLeft, TRight, TResult> joinFunction,
            StreamJoinConfiguration configuration = null,
            IDataStore<TKey, List<BufferedElement<TLeft>>> leftStateStore = null,
            IDataStore<TKey, List<BufferedElement<TRight>>> rightStateStore = null)
        {
            _leftKeySelector = leftKeySelector ?? throw new ArgumentNullException(nameof(leftKeySelector));
            _rightKeySelector = rightKeySelector ?? throw new ArgumentNullException(nameof(rightKeySelector));
            _leftTimestampSelector = leftTimestampSelector ?? throw new ArgumentNullException(nameof(leftTimestampSelector));
            _rightTimestampSelector = rightTimestampSelector ?? throw new ArgumentNullException(nameof(rightTimestampSelector));
            _joinFunction = joinFunction ?? throw new ArgumentNullException(nameof(joinFunction));
            _configuration = configuration ?? StreamJoinConfiguration.Default;

            _leftBuffer = new ConcurrentDictionary<TKey, List<BufferedElement<TLeft>>>();
            _rightBuffer = new ConcurrentDictionary<TKey, List<BufferedElement<TRight>>>();

            _leftStateStore = leftStateStore;
            _rightStateStore = rightStateStore;

            // Start cleanup timer
            _cleanupTimer = new Timer(
                CleanupExpiredElements,
                null,
                _configuration.CleanupInterval,
                _configuration.CleanupInterval);
        }

        /// <summary>
        /// Processes an incoming element. Determines if it's from the left or right stream
        /// and routes accordingly.
        /// </summary>
        public void Process(object input)
        {
            if (input is LeftStreamWrapper<TLeft> leftWrapper)
            {
                ProcessLeft(leftWrapper.Element);
            }
            else if (input is RightStreamWrapper<TRight> rightWrapper)
            {
                ProcessRight(rightWrapper.Element);
            }
            else if (input is TLeft leftElement)
            {
                // Direct left element (default behavior)
                ProcessLeft(leftElement);
            }
            // If it's neither, ignore (could be from other branches)
        }

        /// <summary>
        /// Processes an element from the left stream.
        /// </summary>
        public void ProcessLeft(TLeft left)
        {
            var operatorName = $"StreamStreamJoinOperator<{typeof(TLeft).Name},{typeof(TRight).Name}>.ProcessLeft";
            var stopwatch = _telemetryProvider != null ? Stopwatch.StartNew() : null;

            try
            {
                ErrorHandlingHelper.TryExecute(
                    _executionOptions,
                    operatorName,
                    left,
                    () =>
                    {
                        ProcessLeftInternal(left);
                        return left;
                    });
            }
            finally
            {
                if (stopwatch != null)
                {
                    stopwatch.Stop();
                    RecordProcessingTime(stopwatch.Elapsed.TotalMilliseconds);
                }
                IncrementLeftProcessed();
            }
        }

        /// <summary>
        /// Processes an element from the right stream.
        /// </summary>
        public void ProcessRight(TRight right)
        {
            var operatorName = $"StreamStreamJoinOperator<{typeof(TLeft).Name},{typeof(TRight).Name}>.ProcessRight";
            var stopwatch = _telemetryProvider != null ? Stopwatch.StartNew() : null;

            try
            {
                ErrorHandlingHelper.TryExecute(
                    _executionOptions,
                    operatorName,
                    right,
                    () =>
                    {
                        ProcessRightInternal(right);
                        return right;
                    });
            }
            finally
            {
                if (stopwatch != null)
                {
                    stopwatch.Stop();
                    RecordProcessingTime(stopwatch.Elapsed.TotalMilliseconds);
                }
                IncrementRightProcessed();
            }
        }

        private void ProcessLeftInternal(TLeft left)
        {
            var key = _leftKeySelector(left);
            var timestamp = _leftTimestampSelector(left);
            var bufferedElement = new BufferedElement<TLeft>(left, timestamp, _configuration.WindowSize);

            List<TResult> results = new List<TResult>();
            bool foundMatch = false;

            lock (_bufferLock)
            {
                // Add to left buffer
                var leftList = _leftBuffer.GetOrAdd(key, _ => new List<BufferedElement<TLeft>>());
                
                // Enforce max buffer size
                if (leftList.Count >= _configuration.MaxBufferSizePerKey)
                {
                    leftList.RemoveAt(0); // Remove oldest
                }
                leftList.Add(bufferedElement);

                // Persist to state store if available
                _leftStateStore?.Put(key, leftList);

                // Look for matches in right buffer
                if (_rightBuffer.TryGetValue(key, out var rightList))
                {
                    var now = DateTime.UtcNow;
                    foreach (var rightElement in rightList.ToList())
                    {
                        // Check if within window (not expired)
                        if (rightElement.ExpiresAt > now)
                        {
                            // Found a match!
                            bufferedElement.Matched = true;
                            rightElement.Matched = true;
                            foundMatch = true;

                            var result = _joinFunction(left, rightElement.Element);
                            results.Add(result);
                            IncrementMatched();
                        }
                    }
                }
            }

            // Emit results outside the lock
            foreach (var result in results)
            {
                _nextOperator?.Process(result);
            }

            // For left/outer joins with immediate emission on no match - we'll handle this at window expiration
            // This ensures we give time for right elements to arrive
        }

        private void ProcessRightInternal(TRight right)
        {
            var key = _rightKeySelector(right);
            var timestamp = _rightTimestampSelector(right);
            var bufferedElement = new BufferedElement<TRight>(right, timestamp, _configuration.WindowSize);

            List<TResult> results = new List<TResult>();
            bool foundMatch = false;

            lock (_bufferLock)
            {
                // Add to right buffer
                var rightList = _rightBuffer.GetOrAdd(key, _ => new List<BufferedElement<TRight>>());
                
                // Enforce max buffer size
                if (rightList.Count >= _configuration.MaxBufferSizePerKey)
                {
                    rightList.RemoveAt(0); // Remove oldest
                }
                rightList.Add(bufferedElement);

                // Persist to state store if available
                _rightStateStore?.Put(key, rightList);

                // Look for matches in left buffer
                if (_leftBuffer.TryGetValue(key, out var leftList))
                {
                    var now = DateTime.UtcNow;
                    foreach (var leftElement in leftList.ToList())
                    {
                        // Check if within window (not expired)
                        if (leftElement.ExpiresAt > now)
                        {
                            // Found a match!
                            bufferedElement.Matched = true;
                            leftElement.Matched = true;
                            foundMatch = true;

                            var result = _joinFunction(leftElement.Element, right);
                            results.Add(result);
                            IncrementMatched();
                        }
                    }
                }
            }

            // Emit results outside the lock
            foreach (var result in results)
            {
                _nextOperator?.Process(result);
            }
        }

        /// <summary>
        /// Cleans up expired elements from buffers and emits unmatched results for left/outer joins.
        /// </summary>
        private void CleanupExpiredElements(object state)
        {
            if (_disposed) return;

            var now = DateTime.UtcNow;
            var expiredThreshold = now.Subtract(_configuration.GracePeriod);

            lock (_bufferLock)
            {
                // Process left buffer
                foreach (var kvp in _leftBuffer.ToList())
                {
                    var key = kvp.Key;
                    var leftList = kvp.Value;

                    var expiredElements = leftList
                        .Where(e => e.ExpiresAt <= expiredThreshold)
                        .ToList();

                    foreach (var expired in expiredElements)
                    {
                        leftList.Remove(expired);
                        IncrementExpired();

                        // For left/outer joins, emit unmatched left elements
                        if (!expired.Matched &&
                            (_configuration.JoinType == StreamJoinType.Left ||
                             _configuration.JoinType == StreamJoinType.Outer))
                        {
                            var result = _joinFunction(expired.Element, default);
                            IncrementLeftUnmatched();
                            _nextOperator?.Process(result);
                        }
                    }

                    // Remove empty key entries
                    if (leftList.Count == 0)
                    {
                        _leftBuffer.TryRemove(key, out _);
                    }

                    // Update state store
                    if (_leftStateStore != null)
                    {
                        if (leftList.Count > 0)
                            _leftStateStore.Put(key, leftList);
                        else
                            _leftStateStore.Remove(key);
                    }
                }

                // Process right buffer
                foreach (var kvp in _rightBuffer.ToList())
                {
                    var key = kvp.Key;
                    var rightList = kvp.Value;

                    var expiredElements = rightList
                        .Where(e => e.ExpiresAt <= expiredThreshold)
                        .ToList();

                    foreach (var expired in expiredElements)
                    {
                        rightList.Remove(expired);
                        IncrementExpired();

                        // For right/outer joins, emit unmatched right elements
                        if (!expired.Matched &&
                            (_configuration.JoinType == StreamJoinType.Right ||
                             _configuration.JoinType == StreamJoinType.Outer))
                        {
                            var result = _joinFunction(default, expired.Element);
                            IncrementRightUnmatched();
                            _nextOperator?.Process(result);
                        }
                    }

                    // Remove empty key entries
                    if (rightList.Count == 0)
                    {
                        _rightBuffer.TryRemove(key, out _);
                    }

                    // Update state store
                    if (_rightStateStore != null)
                    {
                        if (rightList.Count > 0)
                            _rightStateStore.Put(key, rightList);
                        else
                            _rightStateStore.Remove(key);
                    }
                }
            }
        }

        /// <summary>
        /// Forces cleanup of expired elements (useful for testing).
        /// </summary>
        public void ForceCleanup()
        {
            CleanupExpiredElements(null);
        }

        /// <summary>
        /// Gets the current count of buffered left elements.
        /// </summary>
        public int GetLeftBufferCount()
        {
            lock (_bufferLock)
            {
                return _leftBuffer.Values.Sum(list => list.Count);
            }
        }

        /// <summary>
        /// Gets the current count of buffered right elements.
        /// </summary>
        public int GetRightBufferCount()
        {
            lock (_bufferLock)
            {
                return _rightBuffer.Values.Sum(list => list.Count);
            }
        }

        #region IOperator Implementation

        public void SetNext(IOperator nextOperator)
        {
            _nextOperator = nextOperator;

            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled && _telemetryProvider != null)
            {
                nextTelemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }

            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }
        }

        #endregion

        #region IStatefulOperator Implementation

        public IEnumerable<IDataStore> GetStateStores()
        {
            var stores = new List<IDataStore>();
            if (_leftStateStore != null) stores.Add(_leftStateStore);
            if (_rightStateStore != null) stores.Add(_rightStateStore);
            return stores;
        }

        #endregion

        #region ITelemetryEnabled Implementation

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                var baseName = $"stream_stream_join_{typeof(TLeft).Name}_{typeof(TRight).Name}";

                _leftProcessedCounter = metricsProvider.CreateCounter(
                    $"{baseName}_left_processed",
                    "Number of left stream elements processed");
                _rightProcessedCounter = metricsProvider.CreateCounter(
                    $"{baseName}_right_processed",
                    "Number of right stream elements processed");
                _matchedCounter = metricsProvider.CreateCounter(
                    $"{baseName}_matched",
                    "Number of successful join matches");
                _leftUnmatchedCounter = metricsProvider.CreateCounter(
                    $"{baseName}_left_unmatched",
                    "Number of left elements that expired without a match");
                _rightUnmatchedCounter = metricsProvider.CreateCounter(
                    $"{baseName}_right_unmatched",
                    "Number of right elements that expired without a match");
                _expiredCounter = metricsProvider.CreateCounter(
                    $"{baseName}_expired",
                    "Number of elements that expired from the window");
                _processingTimeHistogram = metricsProvider.CreateHistogram(
                    $"{baseName}_processing_time",
                    "Processing time for join operations");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"StreamStreamJoinOperator_{typeof(TLeft).Name}_{typeof(TRight).Name}");
            }

            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled)
            {
                nextTelemetryEnabled.SetTelemetryProvider(telemetryProvider);
            }
        }

        #endregion

        #region IErrorHandlingEnabled Implementation

        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;

            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }
        }

        #endregion

        #region Telemetry Helpers

        private void IncrementLeftProcessed() => _leftProcessedCounter?.Increment();
        private void IncrementRightProcessed() => _rightProcessedCounter?.Increment();
        private void IncrementMatched() => _matchedCounter?.Increment();
        private void IncrementLeftUnmatched() => _leftUnmatchedCounter?.Increment();
        private void IncrementRightUnmatched() => _rightUnmatchedCounter?.Increment();
        private void IncrementExpired() => _expiredCounter?.Increment();
        private void RecordProcessingTime(double milliseconds) => _processingTimeHistogram?.Record(milliseconds);

        #endregion

        #region IDisposable Implementation

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
                    _cleanupTimer?.Dispose();
                    _cleanupTimer = null;
                }
                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Wrapper to identify elements from the left stream.
    /// </summary>
    public class LeftStreamWrapper<T>
    {
        public T Element { get; }

        public LeftStreamWrapper(T element)
        {
            Element = element;
        }
    }

    /// <summary>
    /// Wrapper to identify elements from the right stream.
    /// </summary>
    public class RightStreamWrapper<T>
    {
        public T Element { get; }

        public RightStreamWrapper(T element)
        {
            Element = element;
        }
    }
}
