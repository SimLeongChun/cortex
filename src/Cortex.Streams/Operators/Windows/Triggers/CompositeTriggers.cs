using System;

namespace Cortex.Streams.Operators.Windows.Triggers
{
    /// <summary>
    /// A composite trigger that fires when either of two triggers fires.
    /// Useful for combining count and time-based triggers.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public class OrTrigger<TInput> : IWindowTrigger<TInput>
    {
        private readonly IWindowTrigger<TInput> _trigger1;
        private readonly IWindowTrigger<TInput> _trigger2;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrTrigger{TInput}"/> class.
        /// </summary>
        /// <param name="trigger1">The first trigger.</param>
        /// <param name="trigger2">The second trigger.</param>
        public OrTrigger(IWindowTrigger<TInput> trigger1, IWindowTrigger<TInput> trigger2)
        {
            _trigger1 = trigger1 ?? throw new ArgumentNullException(nameof(trigger1));
            _trigger2 = trigger2 ?? throw new ArgumentNullException(nameof(trigger2));
        }

        /// <inheritdoc/>
        public string Description => $"OrTrigger: ({_trigger1.Description}) OR ({_trigger2.Description})";

        /// <inheritdoc/>
        public TriggerResult OnElement(TInput element, DateTime timestamp, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            var result1 = _trigger1.OnElement(element, timestamp, windowStart, windowEnd, context);
            var result2 = _trigger2.OnElement(element, timestamp, windowStart, windowEnd, context);

            return CombineResults(result1, result2);
        }

        /// <inheritdoc/>
        public TriggerResult OnProcessingTime(DateTime processingTime, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            var result1 = _trigger1.OnProcessingTime(processingTime, windowStart, windowEnd, context);
            var result2 = _trigger2.OnProcessingTime(processingTime, windowStart, windowEnd, context);

            return CombineResults(result1, result2);
        }

        /// <inheritdoc/>
        public void Clear(DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            _trigger1.Clear(windowStart, windowEnd, context);
            _trigger2.Clear(windowStart, windowEnd, context);
        }

        private static TriggerResult CombineResults(TriggerResult result1, TriggerResult result2)
        {
            // FireAndPurge takes precedence
            if (result1 == TriggerResult.FireAndPurge || result2 == TriggerResult.FireAndPurge)
            {
                return TriggerResult.FireAndPurge;
            }

            // Fire takes precedence over Continue
            if (result1 == TriggerResult.Fire || result2 == TriggerResult.Fire)
            {
                return TriggerResult.Fire;
            }

            return TriggerResult.Continue;
        }
    }

    /// <summary>
    /// A composite trigger that fires only when both triggers fire.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public class AndTrigger<TInput> : IWindowTrigger<TInput>
    {
        private readonly IWindowTrigger<TInput> _trigger1;
        private readonly IWindowTrigger<TInput> _trigger2;
        private const string Trigger1FiredKey = "AndTrigger_Trigger1Fired";
        private const string Trigger2FiredKey = "AndTrigger_Trigger2Fired";

        /// <summary>
        /// Initializes a new instance of the <see cref="AndTrigger{TInput}"/> class.
        /// </summary>
        /// <param name="trigger1">The first trigger.</param>
        /// <param name="trigger2">The second trigger.</param>
        public AndTrigger(IWindowTrigger<TInput> trigger1, IWindowTrigger<TInput> trigger2)
        {
            _trigger1 = trigger1 ?? throw new ArgumentNullException(nameof(trigger1));
            _trigger2 = trigger2 ?? throw new ArgumentNullException(nameof(trigger2));
        }

        /// <inheritdoc/>
        public string Description => $"AndTrigger: ({_trigger1.Description}) AND ({_trigger2.Description})";

        /// <inheritdoc/>
        public TriggerResult OnElement(TInput element, DateTime timestamp, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            var result1 = _trigger1.OnElement(element, timestamp, windowStart, windowEnd, context);
            var result2 = _trigger2.OnElement(element, timestamp, windowStart, windowEnd, context);

            return EvaluateAndResults(result1, result2, context);
        }

        /// <inheritdoc/>
        public TriggerResult OnProcessingTime(DateTime processingTime, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            var result1 = _trigger1.OnProcessingTime(processingTime, windowStart, windowEnd, context);
            var result2 = _trigger2.OnProcessingTime(processingTime, windowStart, windowEnd, context);

            return EvaluateAndResults(result1, result2, context);
        }

        /// <inheritdoc/>
        public void Clear(DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            _trigger1.Clear(windowStart, windowEnd, context);
            _trigger2.Clear(windowStart, windowEnd, context);
        }

        private TriggerResult EvaluateAndResults(TriggerResult result1, TriggerResult result2, ITriggerContext<TInput> context)
        {
            // Track which triggers have fired
            if (result1 != TriggerResult.Continue)
            {
                context.SetState(Trigger1FiredKey, true);
            }
            if (result2 != TriggerResult.Continue)
            {
                context.SetState(Trigger2FiredKey, true);
            }

            var trigger1Fired = context.GetState<bool>(Trigger1FiredKey);
            var trigger2Fired = context.GetState<bool>(Trigger2FiredKey);

            if (trigger1Fired && trigger2Fired)
            {
                // Reset the state
                context.SetState(Trigger1FiredKey, false);
                context.SetState(Trigger2FiredKey, false);

                // Return the most aggressive result
                if (result1 == TriggerResult.FireAndPurge || result2 == TriggerResult.FireAndPurge)
                {
                    return TriggerResult.FireAndPurge;
                }
                return TriggerResult.Fire;
            }

            return TriggerResult.Continue;
        }
    }
}
