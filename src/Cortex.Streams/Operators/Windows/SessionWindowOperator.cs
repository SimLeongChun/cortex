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
    /// A window operator that groups items into session windows based on inactivity gaps.
    /// A new session window is created when the gap between events exceeds the configured inactivity gap.
    /// </summary>
    /// <typeparam name="TInput">The type of the input items.</typeparam>
    /// <typeparam name="TKey">The type of the key used to partition sessions.</typeparam>
    public class SessionWindowOperator<TInput, TKey> : IOperator, IStatefulOperator, ITelemetryEnabled, IDisposable
    {
        private readonly Func<TInput, TKey> _keySelector;
        private readonly Func<TInput, DateTime> _timestampSelector;
        private readonly TimeSpan _inactivityGap;
        private readonly IDataStore<string, SessionState<TInput>> _stateStore;
        private readonly object _lock = new object();
        private IOperator _nextOperator;
        private Timer _sessionTimer;
        private bool _disposed;

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionWindowOperator{TInput, TKey}"/> class.
        /// </summary>
        /// <param name="keySelector">A function to extract the key from each input item.</param>
        /// <param name="timestampSelector">A function to extract the timestamp from each input item.</param>
        /// <param name="inactivityGap">The duration of inactivity after which a session is closed.</param>
        /// <param name="stateStore">The state store to use for storing session data.</param>
        public SessionWindowOperator(
            Func<TInput, TKey> keySelector,
            Func<TInput, DateTime> timestampSelector,
            TimeSpan inactivityGap,
            IDataStore<string, SessionState<TInput>> stateStore)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _timestampSelector = timestampSelector ?? throw new ArgumentNullException(nameof(timestampSelector));
            _inactivityGap = inactivityGap;
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));

            // Start session evaluation timer
            _sessionTimer = new Timer(EvaluateSessions, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        }

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _processedCounter = metricsProvider.CreateCounter($"session_window_operator_processed_{typeof(TInput).Name}", "Number of items processed by SessionWindowOperator");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"session_window_operator_processing_time_{typeof(TInput).Name}", "Processing time for SessionWindowOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"SessionWindowOperator_{typeof(TInput).Name}");

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
                using (var span = _tracer.StartSpan("SessionWindowOperator.Process"))
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
            var sessionKey = GetSessionKey(key);

            lock (_lock)
            {
                var session = _stateStore.Get(sessionKey);

                if (session == null)
                {
                    // Create new session
                    session = new SessionState<TInput>
                    {
                        Key = key.ToString(),
                        StartTime = timestamp,
                        LastActivityTime = timestamp,
                        Items = new List<TInput> { typedInput }
                    };
                    _stateStore.Put(sessionKey, session);
                }
                else
                {
                    // Check if the event is within the inactivity gap
                    var timeSinceLastActivity = timestamp - session.LastActivityTime;

                    if (timeSinceLastActivity > _inactivityGap)
                    {
                        // Close the current session and emit it
                        EmitSession(sessionKey, session);

                        // Start a new session
                        session = new SessionState<TInput>
                        {
                            Key = key.ToString(),
                            StartTime = timestamp,
                            LastActivityTime = timestamp,
                            Items = new List<TInput> { typedInput }
                        };
                        _stateStore.Put(sessionKey, session);
                    }
                    else
                    {
                        // Extend the current session
                        session.Items.Add(typedInput);
                        session.LastActivityTime = timestamp;
                        _stateStore.Put(sessionKey, session);
                    }
                }
            }
        }

        private string GetSessionKey(TKey key)
        {
            return $"session_{key}";
        }

        private void EmitSession(string sessionKey, SessionState<TInput> session)
        {
            if (session != null && session.Items.Count > 0)
            {
                var windowResult = new WindowResult<string, TInput>(
                    session.Key,
                    session.StartTime,
                    session.LastActivityTime + _inactivityGap,
                    session.Items);

                _nextOperator?.Process(windowResult);
            }
        }

        private void EvaluateSessions(object state)
        {
            var now = DateTime.UtcNow;
            List<string> expiredSessions = new List<string>();

            lock (_lock)
            {
                foreach (var kvp in _stateStore.GetAll())
                {
                    var session = kvp.Value;
                    var timeSinceLastActivity = now - session.LastActivityTime;

                    if (timeSinceLastActivity > _inactivityGap)
                    {
                        expiredSessions.Add(kvp.Key);
                    }
                }

                foreach (var sessionKey in expiredSessions)
                {
                    var session = _stateStore.Get(sessionKey);
                    if (session != null)
                    {
                        EmitSession(sessionKey, session);
                        _stateStore.Remove(sessionKey);
                    }
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
                    _sessionTimer?.Dispose();
                    _sessionTimer = null;
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents the state of a session window.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the session.</typeparam>
    public class SessionState<TInput>
    {
        /// <summary>
        /// Gets or sets the key that identifies this session.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the start time of the session.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the time of the last activity in the session.
        /// </summary>
        public DateTime LastActivityTime { get; set; }

        /// <summary>
        /// Gets or sets the items in the session.
        /// </summary>
        public List<TInput> Items { get; set; }
    }
}
