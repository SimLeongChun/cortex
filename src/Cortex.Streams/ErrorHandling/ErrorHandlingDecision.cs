namespace Cortex.Streams.ErrorHandling
{
    /// <summary>
    /// Per-error decision returned by OnError() or derived from the global strategy.
    /// </summary>
    public enum ErrorHandlingDecision
    {
        Rethrow = 0,
        Skip = 1,
        Retry = 2,
        Stop = 3
    }
}
