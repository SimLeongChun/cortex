using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Processors
{
    /// <summary>
    /// Defines a pre-processor that runs before the request handler is executed.
    /// Pre-processors are useful for validation, authorization, logging, or data enrichment.
    /// </summary>
    /// <typeparam name="TRequest">The type of request being processed.</typeparam>
    public interface IRequestPreProcessor<in TRequest>
    {
        /// <summary>
        /// Executes before the request handler.
        /// </summary>
        /// <param name="request">The request being processed.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ProcessAsync(TRequest request, CancellationToken cancellationToken);
    }
}
