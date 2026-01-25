using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Processors
{
    /// <summary>
    /// Defines a post-processor that runs after the request handler has executed.
    /// Post-processors are useful for logging, cleanup, or triggering side effects.
    /// </summary>
    /// <typeparam name="TRequest">The type of request being processed.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
    public interface IRequestPostProcessor<in TRequest, in TResponse>
    {
        /// <summary>
        /// Executes after the request handler has completed successfully.
        /// </summary>
        /// <param name="request">The request that was processed.</param>
        /// <param name="response">The response returned by the handler.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ProcessAsync(TRequest request, TResponse response, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Defines a post-processor for void commands that don't return a value.
    /// </summary>
    /// <typeparam name="TRequest">The type of request being processed.</typeparam>
    public interface IRequestPostProcessor<in TRequest>
    {
        /// <summary>
        /// Executes after the request handler has completed successfully.
        /// </summary>
        /// <param name="request">The request that was processed.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ProcessAsync(TRequest request, CancellationToken cancellationToken);
    }
}
