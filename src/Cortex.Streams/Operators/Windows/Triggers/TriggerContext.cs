using System;
using System.Collections.Generic;

namespace Cortex.Streams.Operators.Windows.Triggers
{
    /// <summary>
    /// Default implementation of the trigger context.
    /// </summary>
    /// <typeparam name="TInput">The type of items in the window.</typeparam>
    public class TriggerContext<TInput> : ITriggerContext<TInput>
    {
        private readonly Dictionary<string, object> _state = new Dictionary<string, object>();
        private readonly Func<int> _elementCountProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="TriggerContext{TInput}"/> class.
        /// </summary>
        /// <param name="windowKey">The window key.</param>
        /// <param name="elementCountProvider">A function that provides the current element count.</param>
        public TriggerContext(string windowKey, Func<int> elementCountProvider)
        {
            WindowKey = windowKey ?? throw new ArgumentNullException(nameof(windowKey));
            _elementCountProvider = elementCountProvider ?? throw new ArgumentNullException(nameof(elementCountProvider));
        }

        /// <inheritdoc/>
        public int ElementCount => _elementCountProvider();

        /// <inheritdoc/>
        public string WindowKey { get; }

        /// <inheritdoc/>
        public DateTime CurrentProcessingTime => DateTime.UtcNow;

        /// <inheritdoc/>
        public TState GetState<TState>(string key)
        {
            if (_state.TryGetValue(key, out var value))
            {
                return (TState)value;
            }
            return default;
        }

        /// <inheritdoc/>
        public void SetState<TState>(string key, TState value)
        {
            _state[key] = value;
        }

        /// <inheritdoc/>
        public void ClearState()
        {
            _state.Clear();
        }
    }
}
