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
    /// An operator that performs session window aggregation.
    /// </summary>
    /// <typeparam name="TInput">The type of input data.</typeparam>
    /// <typeparam name="TKey">The type of the key to group by.</typeparam>
    /// <typeparam name="TSessionOutput">The type of the output after session windowing.</typeparam>
    public class SessionWindowOperator<TInput, TKey, TSessionOutput> :
        IOperator,
        IStatefulOperator,
        ITelemetryEnabled,
        IErrorHandlingEnabled
    {
        private readonly Func<TInput, TKey> _keySelector;
        private readonly TimeSpan _inactivityGap;
        private readonly Func<IEnumerable<TInput>, TSessionOutput> _sessionFunction;
        private readonly IDataStore<TKey, SessionState<TInput>> _sessionStateStore;
        private readonly IDataStore<SessionKey<TKey>, TSessionOutput> _sessionResultsStateStore;
        private IOperator _nextOperator;

        // Telemetry fields
        private ITelemetryProvider _telemetryProvider;
        private ICounter _processedCounter;
        private IHistogram _processingTimeHistogram;
        private ITracer _tracer;
        private Action _incrementProcessedCounter;
        private Action<double> _recordProcessingTime;

        // Timer + locking for session expiration
        private readonly Timer _sessionExpirationTimer;
        private readonly object _stateLock = new object();

        // Global error handling options
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        public SessionWindowOperator(
            Func<TInput, TKey> keySelector,
            TimeSpan inactivityGap,
            Func<IEnumerable<TInput>, TSessionOutput> sessionFunction,
            IDataStore<TKey, SessionState<TInput>> sessionStateStore,
            IDataStore<SessionKey<TKey>, TSessionOutput> sessionResultsStateStore = null)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _inactivityGap = inactivityGap;
            _sessionFunction = sessionFunction ?? throw new ArgumentNullException(nameof(sessionFunction));
            _sessionStateStore = sessionStateStore ?? throw new ArgumentNullException(nameof(sessionStateStore));
            _sessionResultsStateStore = sessionResultsStateStore;

            // Periodically check for inactive sessions
            _sessionExpirationTimer = new Timer(SessionExpirationCallback, null, inactivityGap, inactivityGap);
        }

        #region Telemetry

        public void SetTelemetryProvider(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;

            if (_telemetryProvider != null)
            {
                var metricsProvider = _telemetryProvider.GetMetricsProvider();

                _processedCounter = metricsProvider.CreateCounter(
                    $"SessionWindowOperator_Processed_{typeof(TInput).Name}",
                    "Number of items processed by SessionWindowOperator");

                _processingTimeHistogram = metricsProvider.CreateHistogram(
                    $"SessionWindowOperator_ProcessingTime_{typeof(TInput).Name}",
                    "Processing time for SessionWindowOperator");

                _tracer = _telemetryProvider
                    .GetTracingProvider()
                    .GetTracer($"SessionWindowOperator_{typeof(TInput).Name}");

                _incrementProcessedCounter = () => _processedCounter.Increment();
                _recordProcessingTime = value => _processingTimeHistogram.Record(value);
            }
            else
            {
                _incrementProcessedCounter = null;
                _recordProcessingTime = null;
            }

            // Propagate telemetry to downstream operator
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
                $"SessionWindowOperator<{typeof(TInput).Name},{typeof(TKey).Name},{typeof(TSessionOutput).Name}>";

            bool executedSuccessfully;

            if (_telemetryProvider != null)
            {
                var stopwatch = Stopwatch.StartNew();

                using (var span = _tracer.StartSpan("SessionWindowOperator.Process"))
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
                                return typedInput; // dummy return
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

            // If executedSuccessfully == false → global error handler decided to Skip this element.
        }

        #endregion

        #region Session logic

        private void ProcessInput(TInput input)
        {
            var key = _keySelector(input);
            var currentTime = DateTime.UtcNow;

            lock (_stateLock)
            {
                SessionState<TInput> sessionState;

                if (!_sessionStateStore.ContainsKey(key))
                {
                    // Start a new session
                    sessionState = new SessionState<TInput>
                    {
                        SessionStartTime = currentTime,
                        LastEventTime = currentTime,
                        Events = new List<TInput> { input }
                    };
                    _sessionStateStore.Put(key, sessionState);
                }
                else
                {
                    sessionState = _sessionStateStore.Get(key);

                    var timeSinceLastEvent = currentTime - sessionState.LastEventTime;

                    if (timeSinceLastEvent <= _inactivityGap)
                    {
                        // Same session: just extend it
                        sessionState.LastEventTime = currentTime;
                        sessionState.Events.Add(input);
                        _sessionStateStore.Put(key, sessionState);
                    }
                    else
                    {
                        // Previous session expired → process it
                        ProcessSession(key, sessionState);

                        // Start new session
                        sessionState = new SessionState<TInput>
                        {
                            SessionStartTime = currentTime,
                            LastEventTime = currentTime,
                            Events = new List<TInput> { input }
                        };
                        _sessionStateStore.Put(key, sessionState);
                    }
                }
            }
        }

        private void ProcessSession(TKey key, SessionState<TInput> sessionState)
        {
            // User function: can throw; caller (ProcessInput / timer) is wrapped in ErrorHandlingHelper
            var sessionOutput = _sessionFunction(sessionState.Events);

            if (_sessionResultsStateStore != null)
            {
                var sessionKey = new SessionKey<TKey>
                {
                    Key = key,
                    SessionStartTime = sessionState.SessionStartTime,
                    SessionEndTime = sessionState.LastEventTime
                };

                _sessionResultsStateStore.Put(sessionKey, sessionOutput);
            }

            // Emit downstream
            _nextOperator?.Process(sessionOutput);

            // Remove from state
            _sessionStateStore.Remove(key);
        }

        private void SessionExpirationCallback(object state)
        {
            var operatorName =
                $"SessionWindowOperator<{typeof(TInput).Name},{typeof(TKey).Name},{typeof(TSessionOutput).Name}>.Timer";

            // We don't care about the return value; we only want consistent error routing.
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
                        var allKeys = _sessionStateStore.GetKeys();

                        foreach (var key in allKeys)
                        {
                            var sessionState = _sessionStateStore.Get(key);
                            if (sessionState != null)
                            {
                                var timeSinceLastEvent = currentTime - sessionState.LastEventTime;

                                if (timeSinceLastEvent > _inactivityGap)
                                {
                                    // Session has expired
                                    keysToProcess.Add(key);
                                }
                            }
                        }
                    }

                    // Process expired sessions outside the lock
                    foreach (var key in keysToProcess)
                    {
                        SessionState<TInput> sessionState;

                        lock (_stateLock)
                        {
                            sessionState = _sessionStateStore.Get(key);
                            if (sessionState == null)
                                continue; // already processed/concurrent
                        }

                        ProcessSession(key, sessionState);
                    }

                    return null;
                });
        }

        #endregion

        #region IStatefulOperator

        public IEnumerable<IDataStore> GetStateStores()
        {
            yield return _sessionStateStore;

            if (_sessionResultsStateStore != null)
                yield return _sessionResultsStateStore;
        }

        #endregion

        #region Next operator wiring

        public void SetNext(IOperator nextOperator)
        {
            _nextOperator = nextOperator;

            // Telemetry -> downstream
            if (_nextOperator is ITelemetryEnabled nextTelemetryEnabled && _telemetryProvider != null)
            {
                nextTelemetryEnabled.SetTelemetryProvider(_telemetryProvider);
            }

            // Error handling -> downstream
            if (_nextOperator is IErrorHandlingEnabled nextWithErrorHandling)
            {
                nextWithErrorHandling.SetErrorHandling(_executionOptions);
            }
        }

        #endregion
    }
}
