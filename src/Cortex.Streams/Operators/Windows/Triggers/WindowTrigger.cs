using System;

namespace Cortex.Streams.Operators.Windows.Triggers
{
    /// <summary>
    /// Defines the result of a trigger evaluation.
    /// </summary>
    public enum TriggerResult
    {
        /// <summary>
        /// Do not emit the window, continue accumulating.
        /// </summary>
        Continue,

        /// <summary>
        /// Emit the window contents but keep accumulating (fire and continue).
        /// </summary>
        Fire,

        /// <summary>
        /// Emit the window contents and purge the window state.
        /// </summary>
        FireAndPurge
    }

    /// <summary>
    /// Base interface for window triggers that define when windows should emit results.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public interface IWindowTrigger<TInput>
    {
        /// <summary>
        /// Called when an element is added to the window.
        /// </summary>
        /// <param name="element">The element being added.</param>
        /// <param name="timestamp">The timestamp of the element.</param>
        /// <param name="windowStart">The start time of the window.</param>
        /// <param name="windowEnd">The end time of the window.</param>
        /// <param name="context">The trigger context providing window state information.</param>
        /// <returns>The trigger result indicating whether to fire the window.</returns>
        TriggerResult OnElement(TInput element, DateTime timestamp, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context);

        /// <summary>
        /// Called when processing time advances.
        /// </summary>
        /// <param name="processingTime">The current processing time.</param>
        /// <param name="windowStart">The start time of the window.</param>
        /// <param name="windowEnd">The end time of the window.</param>
        /// <param name="context">The trigger context providing window state information.</param>
        /// <returns>The trigger result indicating whether to fire the window.</returns>
        TriggerResult OnProcessingTime(DateTime processingTime, DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context);

        /// <summary>
        /// Called when the window is being cleared/closed.
        /// </summary>
        /// <param name="windowStart">The start time of the window.</param>
        /// <param name="windowEnd">The end time of the window.</param>
        /// <param name="context">The trigger context.</param>
        void Clear(DateTime windowStart, DateTime windowEnd, ITriggerContext<TInput> context);

        /// <summary>
        /// Gets the description of this trigger for logging purposes.
        /// </summary>
        string Description { get; }
    }

    /// <summary>
    /// Provides context information for trigger evaluation.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public interface ITriggerContext<TInput>
    {
        /// <summary>
        /// Gets the current count of elements in the window.
        /// </summary>
        int ElementCount { get; }

        /// <summary>
        /// Gets the window key.
        /// </summary>
        string WindowKey { get; }

        /// <summary>
        /// Gets the current processing time.
        /// </summary>
        DateTime CurrentProcessingTime { get; }

        /// <summary>
        /// Gets or sets custom state for the trigger.
        /// </summary>
        /// <typeparam name="TState">The type of state.</typeparam>
        /// <param name="key">The state key.</param>
        /// <returns>The state value.</returns>
        TState GetState<TState>(string key);

        /// <summary>
        /// Sets custom state for the trigger.
        /// </summary>
        /// <typeparam name="TState">The type of state.</typeparam>
        /// <param name="key">The state key.</param>
        /// <param name="value">The state value.</param>
        void SetState<TState>(string key, TState value);

        /// <summary>
        /// Clears all trigger-specific state.
        /// </summary>
        void ClearState();
    }
}
