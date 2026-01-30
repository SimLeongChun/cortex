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
    /// An advanced session window operator with support for custom triggers and state modes.
    /// </summary>
    /// <typeparam name="TInput">The type of the input items.</typeparam>
    /// <typeparam name="TKey">The type of the key used to partition sessions.</typeparam>
    public class AdvancedSessionWindowOperator<TInput, TKey> : IOperator, IStatefulOperator, ITelemetryEnabled, IDisposable
    {
        private readonly Func<TInput, TKey> _keySelector;
        private readonly Func<TInput, DateTime> _timestampSelector;
        private readonly TimeSpan _inactivityGap;
        private readonly IDataStore<string, AdvancedSessionState<TInput>> _stateStore;
        private readonly WindowConfiguration<TInput> _config;
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
        /// Initializes a new instance of the <see cref="AdvancedSessionWindowOperator{TInput, TKey}"/> class.
        /// </summary>
        /// <param name="keySelector">A function to extract the key from each input item.</param>
        /// <param name="timestampSelector">A function to extract the timestamp from each input item.</param>
        /// <param name="inactivityGap">The duration of inactivity after which a session is closed.</param>
        /// <param name="stateStore">The state store to use for storing session data.</param>
        /// <param name="config">The window configuration.</param>
        public AdvancedSessionWindowOperator(
            Func<TInput, TKey> keySelector,
            Func<TInput, DateTime> timestampSelector,
            TimeSpan inactivityGap,
            IDataStore<string, AdvancedSessionState<TInput>> stateStore,
            WindowConfiguration<TInput> config = null)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _timestampSelector = timestampSelector ?? throw new ArgumentNullException(nameof(timestampSelector));
            _inactivityGap = inactivityGap;
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _config = config ?? new WindowConfiguration<TInput>();

            // Start session evaluation timer
            _sessionTimer = new Timer(EvaluateSessions, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        }

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();
                _processedCounter = metricsProvider.CreateCounter($"advanced_session_window_operator_processed_{typeof(TInput).Name}", "Number of items processed by AdvancedSessionWindowOperator");
                _processingTimeHistogram = metricsProvider.CreateHistogram($"advanced_session_window_operator_processing_time_{typeof(TInput).Name}", "Processing time for AdvancedSessionWindowOperator");
                _tracer = _telemetryProvider.GetTracingProvider().GetTracer($"AdvancedSessionWindowOperator_{typeof(TInput).Name}");

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
                using (var span = _tracer.StartSpan("AdvancedSessionWindowOperator.Process"))
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
                    session = new AdvancedSessionState<TInput>
                    {
                        Key = key.ToString(),
                        StartTime = timestamp,
                        LastActivityTime = timestamp,
                        Items = new List<TInput> { typedInput },
                        TriggerContext = new TriggerContext<TInput>(sessionKey, () => _stateStore.Get(sessionKey)?.Items?.Count ?? 0),
                        EmissionSequence = 0,
                        LastEmittedItems = new List<TInput>()
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
                        EmitSession(sessionKey, session, isFinal: true);

                        // Start a new session
                        session = new AdvancedSessionState<TInput>
                        {
                            Key = key.ToString(),
                            StartTime = timestamp,
                            LastActivityTime = timestamp,
                            Items = new List<TInput> { typedInput },
                            TriggerContext = new TriggerContext<TInput>(sessionKey, () => _stateStore.Get(sessionKey)?.Items?.Count ?? 0),
                            EmissionSequence = 0,
                            LastEmittedItems = new List<TInput>()
                        };
                        _stateStore.Put(sessionKey, session);
                    }
                    else
                    {
                        // Extend the current session
                        session.Items.Add(typedInput);
                        session.LastActivityTime = timestamp;
                        _stateStore.Put(sessionKey, session);

                        // Evaluate trigger on element
                        var windowEnd = session.LastActivityTime + _inactivityGap;
                        var triggerResult = _config.Trigger.OnElement(typedInput, timestamp, session.StartTime, windowEnd, session.TriggerContext);
                        
                        if (triggerResult == TriggerResult.Fire)
                        {
                            EmitSession(sessionKey, session, isFinal: false);
                        }
                        else if (triggerResult == TriggerResult.FireAndPurge)
                        {
                            EmitSession(sessionKey, session, isFinal: true);
                            _stateStore.Remove(sessionKey);
                        }
                    }
                }
            }
        }

        private string GetSessionKey(TKey key)
        {
            return $"session_{key}";
        }

        private void EmitSession(string sessionKey, AdvancedSessionState<TInput> session, bool isFinal)
        {
            if (session == null || session.Items.Count == 0)
            {
                return;
            }

            session.EmissionSequence++;
            var emissionType = isFinal ? WindowEmissionType.OnTime : WindowEmissionType.Early;
            var windowEnd = session.LastActivityTime + _inactivityGap;

            // Handle state mode
            List<TInput> itemsToEmit;
            switch (_config.StateMode)
            {
                case WindowStateMode.Accumulating:
                    itemsToEmit = new List<TInput>(session.Items);
                    break;

                case WindowStateMode.AccumulatingAndRetracting:
                    // First emit retraction for previous result if there was one
                    if (session.LastEmittedItems.Count > 0)
                    {
                        var retractionResult = new WindowResult<string, TInput>(
                            session.Key,
                            session.StartTime,
                            windowEnd,
                            session.LastEmittedItems,
                            WindowEmissionType.Retraction,
                            false,
                            DateTime.UtcNow,
                            session.EmissionSequence - 1);
                        _nextOperator?.Process(retractionResult);
                    }
                    itemsToEmit = new List<TInput>(session.Items);
                    session.LastEmittedItems = new List<TInput>(itemsToEmit);
                    break;

                case WindowStateMode.Discarding:
                default:
                    // Only emit items since last emission
                    if (session.LastEmittedItems.Count > 0)
                    {
                        itemsToEmit = session.Items.Skip(session.LastEmittedItems.Count).ToList();
                    }
                    else
                    {
                        itemsToEmit = new List<TInput>(session.Items);
                    }
                    session.LastEmittedItems = new List<TInput>(session.Items);
                    break;
            }

            if (itemsToEmit.Count > 0)
            {
                var windowResult = new WindowResult<string, TInput>(
                    session.Key,
                    session.StartTime,
                    windowEnd,
                    itemsToEmit,
                    emissionType,
                    isFinal,
                    DateTime.UtcNow,
                    session.EmissionSequence);

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
                    var windowEnd = session.LastActivityTime + _inactivityGap;

                    // Evaluate processing time trigger
                    var triggerResult = _config.Trigger.OnProcessingTime(now, session.StartTime, windowEnd, session.TriggerContext);

                    if (triggerResult == TriggerResult.Fire)
                    {
                        EmitSession(kvp.Key, session, isFinal: false);
                    }
                    else if (triggerResult == TriggerResult.FireAndPurge || timeSinceLastActivity > _inactivityGap)
                    {
                        expiredSessions.Add(kvp.Key);
                    }
                }

                foreach (var sessionKey in expiredSessions)
                {
                    var session = _stateStore.Get(sessionKey);
                    if (session != null)
                    {
                        EmitSession(sessionKey, session, isFinal: true);
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
    /// Represents the state of an advanced session window with trigger support.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the session.</typeparam>
    public class AdvancedSessionState<TInput>
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

        /// <summary>
        /// Gets or sets the trigger context for this session.
        /// </summary>
        public TriggerContext<TInput> TriggerContext { get; set; }

        /// <summary>
        /// Gets or sets the emission sequence number.
        /// </summary>
        public int EmissionSequence { get; set; }

        /// <summary>
        /// Gets or sets the last emitted items (for state mode tracking).
        /// </summary>
        public List<TInput> LastEmittedItems { get; set; }
    }
}
