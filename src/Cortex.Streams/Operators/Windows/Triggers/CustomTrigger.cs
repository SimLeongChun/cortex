using System;

namespace Cortex.Streams.Operators.Windows.Triggers
{
    /// <summary>
    /// A trigger that fires based on a custom predicate function.
    /// This provides maximum flexibility for defining when windows should emit results.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public class CustomTrigger<TInput> : IWindowTrigger<TInput>
    {
        private readonly Func<TInput, DateTime, DateTime, DateTime, ITriggerContext<TInput>, TriggerResult> _onElement;
        private readonly Func<DateTime, DateTime, DateTime, ITriggerContext<TInput>, TriggerResult> _onProcessingTime;
        private readonly string _description;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomTrigger{TInput}"/> class.
        /// </summary>
        /// <param name="onElement">Function to evaluate when an element is added.</param>
        /// <param name="onProcessingTime">Function to evaluate on processing time advancement.</param>
        /// <param name="description">A description of this trigger.</param>
        public CustomTrigger(
            Func<TInput, DateTime, DateTime, DateTime, ITriggerContext<TInput>, TriggerResult> onElement = null,
            Func<DateTime, DateTime, DateTime, ITriggerContext<TInput>, TriggerResult> onProcessingTime = null,
            string description = "CustomTrigger")
        {
            _onElement = onElement ?? DefaultOnElement;
            _onProcessingTime = onProcessingTime ?? DefaultOnProcessingTime;
            _description = description;
        }

        private static TriggerResult DefaultOnElement(TInput element, DateTime timestamp, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            return TriggerResult.Continue;
        }

        private static TriggerResult DefaultOnProcessingTime(DateTime time, DateTime windowStart, DateTime end, ITriggerContext<TInput> context)
        {
            return time >= end ? TriggerResult.FireAndPurge : TriggerResult.Continue;
        }

        /// <inheritdoc/>
        public string Description => _description;

        /// <inheritdoc/>
        public TriggerResult OnElement(TInput element, DateTime timestamp, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            return _onElement(element, timestamp, windowStart, windowEnd, context);
        }

        /// <inheritdoc/>
        public TriggerResult OnProcessingTime(DateTime processingTime, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            return _onProcessingTime(processingTime, windowStart, windowEnd, context);
        }

        /// <inheritdoc/>
        public void Clear(DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context)
        {
            context?.ClearState();
        }

        /// <summary>
        /// Creates a builder for constructing a custom trigger.
        /// </summary>
        /// <returns>A custom trigger builder.</returns>
        public static CustomTriggerBuilder<TInput> Create()
        {
            return new CustomTriggerBuilder<TInput>();
        }
    }

    /// <summary>
    /// Builder class for creating custom triggers with a fluent API.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public class CustomTriggerBuilder<TInput>
    {
        private Func<TInput, DateTime, DateTime, DateTime, ITriggerContext<TInput>, TriggerResult> _onElement;
        private Func<DateTime, DateTime, DateTime, ITriggerContext<TInput>, TriggerResult> _onProcessingTime;
        private string _description = "CustomTrigger";

        /// <summary>
        /// Sets the function to evaluate when an element is added.
        /// </summary>
        /// <param name="onElement">The function.</param>
        /// <returns>This builder.</returns>
        public CustomTriggerBuilder<TInput> OnElement(
            Func<TInput, DateTime, DateTime, DateTime, ITriggerContext<TInput>, TriggerResult> onElement)
        {
            _onElement = onElement;
            return this;
        }

        /// <summary>
        /// Sets the function to evaluate on processing time advancement.
        /// </summary>
        /// <param name="onProcessingTime">The function.</param>
        /// <returns>This builder.</returns>
        public CustomTriggerBuilder<TInput> OnProcessingTime(
            Func<DateTime, DateTime, DateTime, ITriggerContext<TInput>, TriggerResult> onProcessingTime)
        {
            _onProcessingTime = onProcessingTime;
            return this;
        }

        /// <summary>
        /// Sets a description for the trigger.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <returns>This builder.</returns>
        public CustomTriggerBuilder<TInput> WithDescription(string description)
        {
            _description = description;
            return this;
        }

        /// <summary>
        /// Builds the custom trigger.
        /// </summary>
        /// <returns>The custom trigger.</returns>
        public CustomTrigger<TInput> Build()
        {
            return new CustomTrigger<TInput>(_onElement, _onProcessingTime, _description);
        }
    }
}
