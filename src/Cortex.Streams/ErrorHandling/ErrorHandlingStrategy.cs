namespace Cortex.Streams.ErrorHandling
{
    /// <summary>
    /// Global strategy used when no per-error decision is returned by OnError.
    /// </summary>
    public enum ErrorHandlingStrategy
    {
        /// <summary>
        /// No global handling – current behavior (exceptions propagate).
        /// </summary>
        None = 0,

        /// <summary>
        /// Skip the offending element and continue.
        /// </summary>
        Skip = 1,

        /// <summary>
        /// Retry the offending element up to MaxRetries.
        /// </summary>
        Retry = 2,

        /// <summary>
        /// Stop the stream gracefully.
        /// </summary>
        Stop = 3
    }
}
