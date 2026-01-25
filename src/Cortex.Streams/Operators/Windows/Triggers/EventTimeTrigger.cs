using System;

namespace Cortex.Streams.Operators.Windows.Triggers
{
    /// <summary>
    /// A trigger that fires when the window's end time is reached (default behavior).
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public class EventTimeTrigger<TInput> : IWindowTrigger<TInput>
    {
        /// <inheritdoc/>
        public string Description => "EventTimeTrigger: Fires when window end time is reached";

        /// <inheritdoc/>
        public TriggerResult OnElement(TInput element, DateTime timestamp, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            // Default behavior: don't fire on element arrival
            return TriggerResult.Continue;
        }

        /// <inheritdoc/>
        public TriggerResult OnProcessingTime(DateTime processingTime, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            if (processingTime >= windowEnd)
            {
                return TriggerResult.FireAndPurge;
            }
            return TriggerResult.Continue;
        }

        /// <inheritdoc/>
        public void Clear(DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            context?.ClearState();
        }
    }
}
