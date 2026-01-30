namespace Cortex.Mediator.Streaming
{
    /// <summary>
    /// Represents a streaming query in the CQRS pattern.
    /// Streaming queries return results as an asynchronous stream (IAsyncEnumerable).
    /// Use this for queries that return large datasets that should be processed item by item.
    /// </summary>
    /// <typeparam name="TResult">The type of each item in the result stream.</typeparam>
    public interface IStreamQuery<out TResult>
    {
    }
}
