using Cortex.Mediator.Queries;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Behaviors
{
    /// <summary>
    /// Pipeline behavior for handling exceptions in query execution.
    /// Provides centralized exception handling with optional fallback results.
    /// </summary>
    /// <typeparam name="TQuery">The type of query being handled.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the query.</typeparam>
    public sealed class ExceptionHandlingQueryBehavior<TQuery, TResult> : IQueryPipelineBehavior<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        private readonly ILogger<ExceptionHandlingQueryBehavior<TQuery, TResult>> _logger;
        private readonly IExceptionHandler<TResult>? _exceptionHandlerWithResult;
        private readonly IExceptionHandler? _exceptionHandler;

        public ExceptionHandlingQueryBehavior(
            ILogger<ExceptionHandlingQueryBehavior<TQuery, TResult>> logger,
            IExceptionHandler<TResult>? exceptionHandlerWithResult = null,
            IExceptionHandler? exceptionHandler = null)
        {
            _logger = logger;
            _exceptionHandlerWithResult = exceptionHandlerWithResult;
            _exceptionHandler = exceptionHandler;
        }

        public async Task<TResult> Handle(
            TQuery query,
            QueryHandlerDelegate<TResult> next,
            CancellationToken cancellationToken)
        {
            try
            {
                return await next();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Don't handle cancellation exceptions, just rethrow
                throw;
            }
            catch (Exception ex)
            {
                var queryType = typeof(TQuery);
                var queryName = queryType.Name;

                _logger.LogError(
                    ex,
                    "Exception caught while executing query {QueryName}: {ExceptionMessage}",
                    queryName,
                    ex.Message);

                // Try handler with result first
                if (_exceptionHandlerWithResult != null)
                {
                    var (handled, result) = await _exceptionHandlerWithResult.HandleWithResultAsync(
                        ex, queryType, query, cancellationToken);

                    if (handled)
                    {
                        _logger.LogWarning(
                            "Exception in query {QueryName} was handled with fallback result",
                            queryName);
                        return result!;
                    }
                }

                // Fall back to basic handler
                if (_exceptionHandler != null)
                {
                    var handled = await _exceptionHandler.HandleAsync(
                        ex, queryType, query, cancellationToken);

                    if (handled)
                    {
                        _logger.LogWarning(
                            "Exception in query {QueryName} was handled, returning default result",
                            queryName);
                        return default!;
                    }
                }

                // Rethrow if not handled
                throw;
            }
        }
    }
}
