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
        /// Gets the type of this emission (early, on-time, late, or retraction).
        /// </summary>
        public WindowEmissionType EmissionType { get; }

        /// <summary>
        /// Gets whether this is a final result (window has closed).
        /// </summary>
        public bool IsFinal { get; }

        /// <summary>
        /// Gets the emission timestamp (when this result was generated).
        /// </summary>
        public DateTime EmissionTime { get; }

        /// <summary>
        /// Gets the sequence number of this emission for the window (useful for tracking updates).
        /// </summary>
        public int EmissionSequence { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowResult{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="key">The key that identifies this window partition.</param>
        /// <param name="windowStart">The start time of the window.</param>
        /// <param name="windowEnd">The end time of the window.</param>
        /// <param name="items">The items contained in this window.</param>
        public WindowResult(TKey key, DateTime windowStart, DateTime windowEnd, IReadOnlyList<TValue> items)
            : this(key, windowStart, windowEnd, items, WindowEmissionType.OnTime, true, DateTime.UtcNow, 1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowResult{TKey, TValue}"/> class with emission metadata.
        /// </summary>
        /// <param name="key">The key that identifies this window partition.</param>
        /// <param name="windowStart">The start time of the window.</param>
        /// <param name="windowEnd">The end time of the window.</param>
        /// <param name="items">The items contained in this window.</param>
        /// <param name="emissionType">The type of emission.</param>
        /// <param name="isFinal">Whether this is a final result.</param>
        /// <param name="emissionTime">The time of emission.</param>
        /// <param name="emissionSequence">The sequence number of this emission.</param>
        public WindowResult(
            TKey key, 
            DateTime windowStart, 
            DateTime windowEnd, 
            IReadOnlyList<TValue> items,
            WindowEmissionType emissionType,
            bool isFinal,
            DateTime emissionTime,
            int emissionSequence)
        {
            Key = key;
            WindowStart = windowStart;
            WindowEnd = windowEnd;
            Items = items ?? throw new ArgumentNullException(nameof(items));
            EmissionType = emissionType;
            IsFinal = isFinal;
            EmissionTime = emissionTime;
            EmissionSequence = emissionSequence;
        }

        /// <summary>
        /// Returns a string representation of the window result.
        /// </summary>
        public override string ToString()
        {
            return $"WindowResult[Key={Key}, Start={WindowStart:O}, End={WindowEnd:O}, Count={Items.Count}, Type={EmissionType}, IsFinal={IsFinal}, Seq={EmissionSequence}]";
        }

        /// <summary>
        /// Creates an early emission result with the same window boundaries.
        /// </summary>
        /// <param name="items">The items to include.</param>
        /// <param name="sequence">The emission sequence number.</param>
        /// <returns>A new window result marked as early.</returns>
        public WindowResult<TKey, TValue> AsEarly(IReadOnlyList<TValue> items, int sequence)
        {
            return new WindowResult<TKey, TValue>(Key, WindowStart, WindowEnd, items, WindowEmissionType.Early, false, DateTime.UtcNow, sequence);
        }

        /// <summary>
        /// Creates a retraction result for this window.
        /// </summary>
        /// <returns>A new window result marked as retraction.</returns>
        public WindowResult<TKey, TValue> AsRetraction()
        {
            return new WindowResult<TKey, TValue>(Key, WindowStart, WindowEnd, Items, WindowEmissionType.Retraction, false, DateTime.UtcNow, EmissionSequence);
        }
    }
}
