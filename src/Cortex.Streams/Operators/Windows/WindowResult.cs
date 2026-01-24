using System;
using System.Collections.Generic;

namespace Cortex.Streams.Operators.Windows
{
    /// <summary>
    /// Represents the result of a window operation containing the key, window boundaries, and aggregated items.
    /// </summary>
    /// <typeparam name="TKey">The type of the key used to partition the window.</typeparam>
    /// <typeparam name="TValue">The type of items in the window.</typeparam>
    public class WindowResult<TKey, TValue>
    {
        /// <summary>
        /// Gets the key that identifies this window partition.
        /// </summary>
        public TKey Key { get; }

        /// <summary>
        /// Gets the start time of the window.
        /// </summary>
        public DateTime WindowStart { get; }

        /// <summary>
        /// Gets the end time of the window.
        /// </summary>
        public DateTime WindowEnd { get; }

        /// <summary>
        /// Gets the items contained in this window.
        /// </summary>
        public IReadOnlyList<TValue> Items { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowResult{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="key">The key that identifies this window partition.</param>
        /// <param name="windowStart">The start time of the window.</param>
        /// <param name="windowEnd">The end time of the window.</param>
        /// <param name="items">The items contained in this window.</param>
        public WindowResult(TKey key, DateTime windowStart, DateTime windowEnd, IReadOnlyList<TValue> items)
        {
            Key = key;
            WindowStart = windowStart;
            WindowEnd = windowEnd;
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        /// <summary>
        /// Returns a string representation of the window result.
        /// </summary>
        public override string ToString()
        {
            return $"WindowResult[Key={Key}, Start={WindowStart:O}, End={WindowEnd:O}, Count={Items.Count}]";
        }
    }
}
