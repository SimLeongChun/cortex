using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Behaviors
{
    /// <summary>
    /// Default exception handler that logs exceptions and rethrows them.
    /// </summary>
    public class DefaultExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<DefaultExceptionHandler> _logger;

        public DefaultExceptionHandler(ILogger<DefaultExceptionHandler> logger)
        {
            _logger = logger;
        }

        public Task<bool> HandleAsync(
            Exception exception,
            Type requestType,
            object request,
            CancellationToken cancellationToken)
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred while processing {RequestType}",
                requestType.Name);

            // Return false to indicate the exception should be rethrown
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Default exception handler that logs exceptions and rethrows them,
    /// with support for providing fallback results.
    /// </summary>
    /// <typeparam name="TResult">The type of fallback result.</typeparam>
    public class DefaultExceptionHandler<TResult> : IExceptionHandler<TResult>
    {
        private readonly ILogger<DefaultExceptionHandler<TResult>> _logger;

        public DefaultExceptionHandler(ILogger<DefaultExceptionHandler<TResult>> logger)
        {
            _logger = logger;
        }

        public Task<bool> HandleAsync(
            Exception exception,
            Type requestType,
            object request,
            CancellationToken cancellationToken)
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred while processing {RequestType}",
                requestType.Name);

            // Return false to indicate the exception should be rethrown
            return Task.FromResult(false);
        }

        public Task<(bool handled, TResult? result)> HandleWithResultAsync(
            Exception exception,
            Type requestType,
            object request,
            CancellationToken cancellationToken)
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred while processing {RequestType}",
                requestType.Name);

            // Return false to indicate the exception should be rethrown
            return Task.FromResult<(bool handled, TResult? result)>((false, default));
        }
    }
}
