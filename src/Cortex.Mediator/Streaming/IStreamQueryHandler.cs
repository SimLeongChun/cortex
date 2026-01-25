using System.Collections.Generic;
using System.Threading;

namespace Cortex.Mediator.Streaming
{
    /// <summary>
    /// Defines a handler for a streaming query.
    /// </summary>
    /// <typeparam name="TQuery">The type of streaming query being handled.</typeparam>
    /// <typeparam name="TResult">The type of each item in the result stream.</typeparam>
    public interface IStreamQueryHandler<in TQuery, out TResult>
        where TQuery : IStreamQuery<TResult>
    {
        /// <summary>
        /// Handles the specified streaming query and returns an asynchronous stream of results.
        /// </summary>
        /// <param name="query">The query to handle.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An asynchronous enumerable of results.</returns>
        IAsyncEnumerable<TResult> Handle(TQuery query, CancellationToken cancellationToken);
    }
}
