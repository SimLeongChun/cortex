using Cortex.Mediator.Commands;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Behaviors
{
    /// <summary>
    /// Pipeline behavior for handling exceptions in command execution.
    /// Provides centralized exception handling with optional fallback results.
    /// </summary>
    /// <typeparam name="TCommand">The type of command being handled.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the command.</typeparam>
    public sealed class ExceptionHandlingCommandBehavior<TCommand, TResult> : ICommandPipelineBehavior<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        private readonly ILogger<ExceptionHandlingCommandBehavior<TCommand, TResult>> _logger;
        private readonly IExceptionHandler<TResult>? _exceptionHandlerWithResult;
        private readonly IExceptionHandler? _exceptionHandler;

        public ExceptionHandlingCommandBehavior(
            ILogger<ExceptionHandlingCommandBehavior<TCommand, TResult>> logger,
            IExceptionHandler<TResult>? exceptionHandlerWithResult = null,
            IExceptionHandler? exceptionHandler = null)
        {
            _logger = logger;
            _exceptionHandlerWithResult = exceptionHandlerWithResult;
            _exceptionHandler = exceptionHandler;
        }

        public async Task<TResult> Handle(
            TCommand command,
            CommandHandlerDelegate<TResult> next,
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
                var commandType = typeof(TCommand);
                var commandName = commandType.Name;

                _logger.LogError(
                    ex,
                    "Exception caught while executing command {CommandName}: {ExceptionMessage}",
                    commandName,
                    ex.Message);

                // Try handler with result first
                if (_exceptionHandlerWithResult != null)
                {
                    var (handled, result) = await _exceptionHandlerWithResult.HandleWithResultAsync(
                        ex, commandType, command, cancellationToken);

                    if (handled)
                    {
                        _logger.LogWarning(
                            "Exception in command {CommandName} was handled with fallback result",
                            commandName);
                        return result!;
                    }
                }

                // Fall back to basic handler
                if (_exceptionHandler != null)
                {
                    var handled = await _exceptionHandler.HandleAsync(
                        ex, commandType, command, cancellationToken);

                    if (handled)
                    {
                        _logger.LogWarning(
                            "Exception in command {CommandName} was handled, returning default result",
                            commandName);
                        return default!;
                    }
                }

                // Rethrow if not handled
                throw;
            }
        }
    }
}
