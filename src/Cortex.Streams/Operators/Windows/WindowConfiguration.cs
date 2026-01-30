using Cortex.Streams.Operators.Windows.Triggers;
using System;

namespace Cortex.Streams.Operators.Windows
{
    /// <summary>
    /// Configuration options for advanced windowing operations.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public class WindowConfiguration<TInput>
    {
        /// <summary>
        /// Gets or sets the trigger that determines when windows emit results.
        /// Default is EventTimeTrigger (fires at window end time).
        /// </summary>
        public IWindowTrigger<TInput> Trigger { get; set; }

        /// <summary>
        /// Gets or sets the window state mode that determines how state is managed across firings.
        /// Default is Discarding (state is cleared after each emission).
        /// </summary>
        public WindowStateMode StateMode { get; set; } = WindowStateMode.Discarding;

        /// <summary>
        /// Gets or sets the allowed lateness for late-arriving data.
        /// Events arriving after window end + allowed lateness will be dropped.
        /// Default is TimeSpan.Zero (no late data allowed).
        /// </summary>
        public TimeSpan AllowedLateness { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Gets or sets a callback for dropped late events.
        /// </summary>
        public Action<TInput, DateTime> OnLateEvent { get; set; }

        /// <summary>
        /// Creates a new window configuration with default settings.
        /// </summary>
        public WindowConfiguration()
        {
            Trigger = new EventTimeTrigger<TInput>();
        }

        /// <summary>
        /// Creates a builder for fluent configuration.
        /// </summary>
        /// <returns>A window configuration builder.</returns>
        public static WindowConfigurationBuilder<TInput> Create()
        {
            return new WindowConfigurationBuilder<TInput>();
        }
    }

    /// <summary>
    /// Builder class for creating window configurations with a fluent API.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public class WindowConfigurationBuilder<TInput>
    {
        private readonly WindowConfiguration<TInput> _config = new WindowConfiguration<TInput>();

        /// <summary>
        /// Sets the trigger for the window.
        /// </summary>
        /// <param name="trigger">The trigger to use.</param>
        /// <returns>This builder.</returns>
        public WindowConfigurationBuilder<TInput> WithTrigger(IWindowTrigger<TInput> trigger)
        {
            _config.Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
            return this;
        }

        /// <summary>
        /// Sets a count-based trigger that fires every N elements.
        /// </summary>
        /// <param name="count">The number of elements after which to fire.</param>
        /// <returns>This builder.</returns>
        public WindowConfigurationBuilder<TInput> TriggerOnCount(int count)
        {
            _config.Trigger = new CountTrigger<TInput>(count);
            return this;
        }

        /// <summary>
        /// Sets a processing time trigger that fires at specified intervals.
        /// </summary>
        /// <param name="interval">The interval at which to fire.</param>
        /// <returns>This builder.</returns>
        public WindowConfigurationBuilder<TInput> TriggerOnProcessingTime(TimeSpan interval)
        {
            _config.Trigger = new ProcessingTimeTrigger<TInput>(interval);
            return this;
        }

        /// <summary>
        /// Sets an early trigger that emits partial results at specified intervals.
        /// </summary>
        /// <param name="interval">The interval for early emissions.</param>
        /// <returns>This builder.</returns>
        public WindowConfigurationBuilder<TInput> WithEarlyTrigger(TimeSpan interval)
        {
            _config.Trigger = new EarlyTrigger<TInput>(interval);
            return this;
        }

        /// <summary>
        /// Combines the current trigger with another using OR logic.
        /// </summary>
        /// <param name="trigger">The trigger to combine with.</param>
        /// <returns>This builder.</returns>
        public WindowConfigurationBuilder<TInput> OrTrigger(IWindowTrigger<TInput> trigger)
        {
            _config.Trigger = new OrTrigger<TInput>(_config.Trigger, trigger);
            return this;
        }

        /// <summary>
        /// Sets the window to accumulating mode.
        /// </summary>
        /// <returns>This builder.</returns>
        public WindowConfigurationBuilder<TInput> Accumulating()
        {
            _config.StateMode = WindowStateMode.Accumulating;
            return this;
        }

        /// <summary>
        /// Sets the window to discarding mode.
        /// </summary>
        /// <returns>This builder.</returns>
        public WindowConfigurationBuilder<TInput> Discarding()
        {
            _config.StateMode = WindowStateMode.Discarding;
            return this;
        }

        /// <summary>
        /// Sets the window to accumulating and retracting mode.
        /// </summary>
        /// <returns>This builder.</returns>
        public WindowConfigurationBuilder<TInput> AccumulatingAndRetracting()
        {
            _config.StateMode = WindowStateMode.AccumulatingAndRetracting;
            return this;
        }

        /// <summary>
        /// Sets the allowed lateness for late-arriving data.
        /// </summary>
        /// <param name="lateness">The allowed lateness.</param>
        /// <returns>This builder.</returns>
        public WindowConfigurationBuilder<TInput> WithAllowedLateness(TimeSpan lateness)
        {
            _config.AllowedLateness = lateness;
            return this;
        }

        /// <summary>
        /// Sets a callback for dropped late events.
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <returns>This builder.</returns>
        public WindowConfigurationBuilder<TInput> OnLateEvent(Action<TInput, DateTime> callback)
        {
            _config.OnLateEvent = callback;
            return this;
        }

        /// <summary>
        /// Builds the window configuration.
        /// </summary>
        /// <returns>The window configuration.</returns>
        public WindowConfiguration<TInput> Build()
        {
            return _config;
        }
    }
}
