using System;

namespace Cortex.Streams.ErrorHandling
{
    /// <summary>Context passed into the global OnError callback.</summary>
    public sealed class StreamErrorContext
    {
        public string StreamName { get; }
        public string OperatorName { get; }
        public object Input { get; }
        public Exception Exception { get; }
        public int Attempt { get; }

        public StreamErrorContext(string streamName, string operatorName, object input, Exception exception, int attempt)
        {
            StreamName = streamName;
            OperatorName = operatorName;
            Input = input;
            Exception = exception;
            Attempt = attempt;
        }
    }

    /// <summary>Delegate for the global error callback.</summary>
    public delegate ErrorHandlingDecision StreamErrorHandler(StreamErrorContext context);
}
