using System;

namespace Cortex.Streams.ErrorHandling
{
    /// <summary>
    /// Execution options controlling global error handling for a stream.
    /// </summary>
    public sealed class StreamExecutionOptions
    {
        /// <summary>
        /// Default global strategy applied when OnError is null.
        /// </summary>
        public ErrorHandlingStrategy ErrorHandlingStrategy { get; set; } = ErrorHandlingStrategy.None;

        /// <summary>
        /// Max retries when ErrorHandlingStrategy == Retry.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Optional delay between retry attempts.
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Optional callback invoked whenever an operator throws.
        /// If provided, it can override the global strategy per error.
        /// </summary>
        public StreamErrorHandler OnError { get; set; }

        /// <summary>
        /// Filled in by Stream when the pipeline is built.
        /// </summary>
        internal string StreamName { get; set; }

        /// <summary>
        /// Default execution options with no error handling configured.
        /// </summary>
        public static readonly StreamExecutionOptions Default = new StreamExecutionOptions();
    }
}
