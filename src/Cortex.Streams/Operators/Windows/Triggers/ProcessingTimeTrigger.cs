using System;

namespace Cortex.Streams.Operators.Windows.Triggers
{
    /// <summary>
    /// A trigger that fires at specified time intervals within the window's lifetime.
    /// This enables early/partial results before the window closes.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public class ProcessingTimeTrigger<TInput> : IWindowTrigger<TInput>
    {
        private readonly TimeSpan _interval;
        private const string LastFireTimeKey = "ProcessingTimeTrigger_LastFireTime";

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessingTimeTrigger{TInput}"/> class.
        /// </summary>
        /// <param name="interval">The interval at which to fire the trigger.</param>
        public ProcessingTimeTrigger(TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
            }
            _interval = interval;
        }

        /// <inheritdoc/>
        public string Description => $"ProcessingTimeTrigger: Fires every {_interval.TotalSeconds} seconds";

        /// <inheritdoc/>
        public TriggerResult OnElement(TInput element, DateTime timestamp, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            // Initialize last fire time if not set
            var lastFireTime = context.GetState<DateTime?>(LastFireTimeKey);
            if (lastFireTime == null)
            {
                context.SetState(LastFireTimeKey, (DateTime?)context.CurrentProcessingTime);
            }
            return TriggerResult.Continue;
        }

        /// <inheritdoc/>
        public TriggerResult OnProcessingTime(DateTime processingTime, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            // Window has ended
            if (processingTime >= windowEnd)
            {
                return TriggerResult.FireAndPurge;
            }

            var lastFireTime = context.GetState<DateTime?>(LastFireTimeKey);
            if (lastFireTime == null)
            {
                context.SetState(LastFireTimeKey, (DateTime?)processingTime);
                lastFireTime = processingTime;
            }

            // Check if interval has passed since last fire
            if (processingTime - lastFireTime.Value >= _interval)
            {
                context.SetState(LastFireTimeKey, (DateTime?)processingTime);
                return TriggerResult.Fire;
            }

            return TriggerResult.Continue;
        }

        /// <inheritdoc/>
        public void Clear(DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            context?.ClearState();
        }

        /// <summary>
        /// Creates a processing time trigger that fires at the specified interval.
        /// </summary>
        /// <param name="interval">The interval.</param>
        /// <returns>A new processing time trigger.</returns>
        public static ProcessingTimeTrigger<TInput> Every(TimeSpan interval)
        {
            return new ProcessingTimeTrigger<TInput>(interval);
        }
    }
}
