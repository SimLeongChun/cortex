using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Streaming
{
    /// <summary>
    /// Defines a pipeline behavior for wrapping streaming query handlers.
    /// </summary>
    /// <typeparam name="TQuery">The type of streaming query being handled.</typeparam>
    /// <typeparam name="TResult">The type of each item in the result stream.</typeparam>
    public interface IStreamQueryPipelineBehavior<in TQuery, TResult>
        where TQuery : IStreamQuery<TResult>
    {
        /// <summary>
        /// Handles the streaming query and invokes the next behavior in the pipeline.
        /// This method is called once before the stream begins.
        /// </summary>
        /// <param name="query">The query being handled.</param>
        /// <param name="next">Delegate to invoke the next behavior or handler.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An asynchronous enumerable of results.</returns>
        IAsyncEnumerable<TResult> Handle(
            TQuery query,
            StreamQueryHandlerDelegate<TResult> next,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Represents a delegate that wraps the streaming query handler execution.
    /// </summary>
    /// <typeparam name="TResult">The type of each item in the result stream.</typeparam>
    public delegate IAsyncEnumerable<TResult> StreamQueryHandlerDelegate<out TResult>();
}
