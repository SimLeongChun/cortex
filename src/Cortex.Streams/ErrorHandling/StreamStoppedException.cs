using System;

namespace Cortex.Streams.ErrorHandling
{
    /// <summary>
    /// Special exception that means "stop this stream gracefully".
    /// Consumers of IStream should treat this as a controlled shutdown, not a crash.
    /// </summary>
    public sealed class StreamStoppedException : Exception
    {
        public StreamStoppedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
