using System;

namespace Cortex.Streams.Operators.Windows.Triggers
{
    /// <summary>
    /// A trigger that emits early partial results at specified intervals before the final window close,
    /// useful for long-running windows where users want periodic updates.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public class EarlyTrigger<TInput> : IWindowTrigger<TInput>
    {
        private readonly TimeSpan _earlyInterval;
        private readonly IWindowTrigger<TInput> _lateTrigger;
        private const string LastEarlyFireKey = "EarlyTrigger_LastEarlyFire";
        private const string HasEmittedEarlyKey = "EarlyTrigger_HasEmittedEarly";

        /// <summary>
        /// Initializes a new instance of the <see cref="EarlyTrigger{TInput}"/> class.
        /// </summary>
        /// <param name="earlyInterval">The interval for early emissions.</param>
        /// <param name="lateTrigger">Optional trigger for late firings (after window end). If null, uses default event time trigger.</param>
        public EarlyTrigger(TimeSpan earlyInterval, IWindowTrigger<TInput> lateTrigger = null)
        {
            if (earlyInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(earlyInterval), "Early interval must be greater than zero.");
            }
            _earlyInterval = earlyInterval;
            _lateTrigger = lateTrigger ?? new EventTimeTrigger<TInput>();
        }

        /// <inheritdoc/>
        public string Description => $"EarlyTrigger: Emits early results every {_earlyInterval.TotalSeconds}s, final at window end";

        /// <inheritdoc/>
        public TriggerResult OnElement(TInput element, DateTime timestamp, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            // Initialize last early fire time if not set
            var lastEarlyFire = context.GetState<DateTime?>(LastEarlyFireKey);
            if (lastEarlyFire == null)
            {
                context.SetState(LastEarlyFireKey, (DateTime?)context.CurrentProcessingTime);
            }
            return TriggerResult.Continue;
        }

        /// <inheritdoc/>
        public TriggerResult OnProcessingTime(DateTime processingTime, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            // Window has ended - fire final result
            if (processingTime >= windowEnd)
            {
                return TriggerResult.FireAndPurge;
            }

            var lastEarlyFire = context.GetState<DateTime?>(LastEarlyFireKey);
            if (lastEarlyFire == null)
            {
                context.SetState(LastEarlyFireKey, (DateTime?)processingTime);
                lastEarlyFire = processingTime;
            }

            // Check if it's time for an early emission
            if (processingTime - lastEarlyFire.Value >= _earlyInterval && context.ElementCount > 0)
            {
                context.SetState(LastEarlyFireKey, (DateTime?)processingTime);
                context.SetState(HasEmittedEarlyKey, true);
                return TriggerResult.Fire; // Fire but don't purge - continue accumulating
            }

            return TriggerResult.Continue;
        }

        /// <inheritdoc/>
        public void Clear(DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            _lateTrigger.Clear(windowStart, windowEnd, context);
            context?.ClearState();
        }

        /// <summary>
        /// Creates an early trigger that emits partial results at the specified interval.
        /// </summary>
        /// <param name="interval">The interval for early emissions.</param>
        /// <returns>A new early trigger.</returns>
        public static EarlyTrigger<TInput> Every(TimeSpan interval)
        {
            return new EarlyTrigger<TInput>(interval);
        }
    }
}
