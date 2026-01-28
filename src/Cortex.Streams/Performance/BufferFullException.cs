using System;

namespace Cortex.Streams.Performance
{
    /// <summary>
    /// Exception thrown when the stream buffer is full and the backpressure strategy is set to ThrowException.
    /// </summary>
    public class BufferFullException : Exception
    {
        /// <summary>
        /// The current capacity of the buffer.
        /// </summary>
        public int BufferCapacity { get; }

        /// <summary>
        /// The item that could not be added to the buffer.
        /// </summary>
        public object RejectedItem { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferFullException"/> class.
        /// </summary>
        /// <param name="bufferCapacity">The capacity of the full buffer.</param>
        /// <param name="rejectedItem">The item that was rejected.</param>
        public BufferFullException(int bufferCapacity, object rejectedItem)
            : base($"Stream buffer is full (capacity: {bufferCapacity}). Cannot accept more items.")
        {
            BufferCapacity = bufferCapacity;
            RejectedItem = rejectedItem;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferFullException"/> class with a custom message.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="bufferCapacity">The capacity of the full buffer.</param>
        /// <param name="rejectedItem">The item that was rejected.</param>
        public BufferFullException(string message, int bufferCapacity, object rejectedItem)
            : base(message)
        {
            BufferCapacity = bufferCapacity;
            RejectedItem = rejectedItem;
        }
    }
}
