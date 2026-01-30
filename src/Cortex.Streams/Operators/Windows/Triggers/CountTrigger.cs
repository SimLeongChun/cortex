using System;

namespace Cortex.Streams.Operators.Windows.Triggers
{
    /// <summary>
    /// A trigger that fires when the window contains a specified number of elements.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public class CountTrigger<TInput> : IWindowTrigger<TInput>
    {
        private readonly int _maxCount;
        private const string FiredCountKey = "CountTrigger_FiredCount";

        /// <summary>
        /// Initializes a new instance of the <see cref="CountTrigger{TInput}"/> class.
        /// </summary>
        /// <param name="maxCount">The number of elements after which the trigger fires.</param>
        public CountTrigger(int maxCount)
        {
            if (maxCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), "Count must be greater than 0.");
            }
            _maxCount = maxCount;
        }

        /// <inheritdoc/>
        public string Description => $"CountTrigger: Fires every {_maxCount} elements";

        /// <inheritdoc/>
        public TriggerResult OnElement(TInput element, DateTime timestamp, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            var firedCount = context.GetState<int>(FiredCountKey);
            var newElementsSinceLastFire = context.ElementCount - (firedCount * _maxCount);

            if (newElementsSinceLastFire >= _maxCount)
            {
                context.SetState(FiredCountKey, firedCount + 1);
                return TriggerResult.Fire;
            }

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

        /// <summary>
        /// Creates a count trigger that fires every specified number of elements.
        /// </summary>
        /// <param name="count">The number of elements.</param>
        /// <returns>A new count trigger.</returns>
        public static CountTrigger<TInput> Of(int count)
        {
            return new CountTrigger<TInput>(count);
        }
    }
}
