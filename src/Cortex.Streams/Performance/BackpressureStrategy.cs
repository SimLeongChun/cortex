namespace Cortex.Streams.Performance
{
    /// <summary>
    /// Defines the strategy to handle backpressure when the internal buffer is full.
    /// </summary>
    public enum BackpressureStrategy
    {
        /// <summary>
        /// Block the caller until space is available in the buffer.
        /// This ensures no data loss but may cause the producer to slow down.
        /// </summary>
        Block,

        /// <summary>
        /// Drop the oldest items in the buffer to make room for new items.
        /// This allows the producer to continue at full speed but may lose older data.
        /// </summary>
        DropOldest,

        /// <summary>
        /// Drop the newest items (incoming) when the buffer is full.
        /// This allows the producer to continue without blocking but may lose recent data.
        /// </summary>
        DropNewest,

        /// <summary>
        /// Throw an exception when the buffer is full.
        /// This provides explicit failure feedback to the producer.
        /// </summary>
        ThrowException
    }
}
