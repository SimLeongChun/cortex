using System;

namespace Cortex.Streams.Operators.Windows.Triggers
{
    /// <summary>
    /// Extension methods for composing window triggers.
    /// </summary>
    public static class TriggerExtensions
    {
        /// <summary>
        /// Combines this trigger with another using OR logic.
        /// The combined trigger fires when either trigger would fire.
        /// </summary>
        /// <typeparam name="TInput">The type of items in the window.</typeparam>
        /// <param name="trigger">The first trigger.</param>
        /// <param name="other">The second trigger.</param>
        /// <returns>A combined trigger that fires when either input trigger fires.</returns>
        public static IWindowTrigger<TInput> Or<TInput>(this IWindowTrigger<TInput> trigger, IWindowTrigger<TInput> other)
        {
            return new OrTrigger<TInput>(trigger, other);
        }

        /// <summary>
        /// Combines this trigger with another using AND logic.
        /// The combined trigger fires only when both triggers would fire.
        /// </summary>
        /// <typeparam name="TInput">The type of items in the window.</typeparam>
        /// <param name="trigger">The first trigger.</param>
        /// <param name="other">The second trigger.</param>
        /// <returns>A combined trigger that fires when both input triggers have fired.</returns>
        public static IWindowTrigger<TInput> And<TInput>(this IWindowTrigger<TInput> trigger, IWindowTrigger<TInput> other)
        {
            return new AndTrigger<TInput>(trigger, other);
        }
    }

    /// <summary>
    /// Factory class for creating common trigger configurations.
    /// </summary>
    public static class Triggers
    {
        /// <summary>
        /// Creates a default event time trigger that fires when the window end time is reached.
        /// </summary>
        /// <typeparam name="TInput">The type of items in the window.</typeparam>
        /// <returns>An event time trigger.</returns>
        public static IWindowTrigger<TInput> OnEventTime<TInput>()
        {
            return new EventTimeTrigger<TInput>();
        }

        /// <summary>
        /// Creates a count trigger that fires every N elements.
        /// </summary>
        /// <typeparam name="TInput">The type of items in the window.</typeparam>
        /// <param name="count">The number of elements after which to fire.</param>
        /// <returns>A count trigger.</returns>
        public static IWindowTrigger<TInput> OnCount<TInput>(int count)
        {
            return new CountTrigger<TInput>(count);
        }

        /// <summary>
        /// Creates a processing time trigger that fires at specified intervals.
        /// </summary>
        /// <typeparam name="TInput">The type of items in the window.</typeparam>
        /// <param name="interval">The interval at which to fire.</param>
        /// <returns>A processing time trigger.</returns>
        public static IWindowTrigger<TInput> OnProcessingTime<TInput>(TimeSpan interval)
        {
            return new ProcessingTimeTrigger<TInput>(interval);
        }

        /// <summary>
        /// Creates an early trigger that emits partial results at specified intervals before the final window close.
        /// </summary>
        /// <typeparam name="TInput">The type of items in the window.</typeparam>
        /// <param name="interval">The interval for early emissions.</param>
        /// <returns>An early trigger.</returns>
        public static IWindowTrigger<TInput> WithEarlyFirings<TInput>(TimeSpan interval)
        {
            return new EarlyTrigger<TInput>(interval);
        }

        /// <summary>
        /// Creates a trigger that fires either on count or on time, whichever comes first.
        /// </summary>
        /// <typeparam name="TInput">The type of items in the window.</typeparam>
        /// <param name="count">The number of elements after which to fire.</param>
        /// <param name="interval">The interval at which to fire.</param>
        /// <returns>A combined trigger.</returns>
        public static IWindowTrigger<TInput> OnCountOrTime<TInput>(int count, TimeSpan interval)
        {
            return new OrTrigger<TInput>(
                new CountTrigger<TInput>(count),
                new ProcessingTimeTrigger<TInput>(interval));
        }

        /// <summary>
        /// Creates a custom trigger using the provided functions.
        /// </summary>
        /// <typeparam name="TInput">The type of items in the window.</typeparam>
        /// <param name="onElement">Function to evaluate when an element is added.</param>
        /// <param name="onProcessingTime">Function to evaluate on processing time advancement.</param>
        /// <param name="description">A description of this trigger.</param>
        /// <returns>A custom trigger.</returns>
        public static IWindowTrigger<TInput> Custom<TInput>(
            Func<TInput, DateTime, DateTime, DateTime, ITriggerContext<TInput>, TriggerResult> onElement = null,
            Func<DateTime, DateTime, DateTime, ITriggerContext<TInput>, TriggerResult> onProcessingTime = null,
            string description = "CustomTrigger")
        {
            return new CustomTrigger<TInput>(onElement, onProcessingTime, description);
        }
    }
}
