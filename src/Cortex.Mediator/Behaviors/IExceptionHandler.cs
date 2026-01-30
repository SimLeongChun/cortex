using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Behaviors
{
    /// <summary>
    /// Defines how exceptions should be handled by the exception handling behavior.
    /// </summary>
    public interface IExceptionHandler
    {
        /// <summary>
        /// Handles an exception that occurred during request processing.
        /// </summary>
        /// <param name="exception">The exception that was thrown.</param>
        /// <param name="requestType">The type of the request (command, query, or notification).</param>
        /// <param name="request">The request object that caused the exception.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A task that represents the exception handling operation.
        /// Return true to indicate the exception was handled and should not be rethrown.
        /// Return false to indicate the exception should be rethrown after handling.
        /// </returns>
        Task<bool> HandleAsync(
            Exception exception,
            Type requestType,
            object request,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Defines how exceptions should be handled by the exception handling behavior,
    /// with the ability to provide a fallback result.
    /// </summary>
    /// <typeparam name="TResult">The type of result to return when handling the exception.</typeparam>
    public interface IExceptionHandler<TResult> : IExceptionHandler
    {
        /// <summary>
        /// Handles an exception and optionally provides a fallback result.
        /// </summary>
        /// <param name="exception">The exception that was thrown.</param>
        /// <param name="requestType">The type of the request (command or query).</param>
        /// <param name="request">The request object that caused the exception.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A tuple containing:
        /// - handled: true if the exception was handled and should not be rethrown
        /// - result: the fallback result to return (only used if handled is true)
        /// </returns>
        Task<(bool handled, TResult? result)> HandleWithResultAsync(
            Exception exception,
            Type requestType,
            object request,
            CancellationToken cancellationToken);
    }
}
