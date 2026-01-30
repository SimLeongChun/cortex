using Cortex.Mediator.Commands;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Behaviors
{
    /// <summary>
    /// Pipeline behavior for handling exceptions in void command execution.
    /// Provides centralized exception handling for commands that don't return a value.
    /// </summary>
    /// <typeparam name="TCommand">The type of command being handled.</typeparam>
    public sealed class ExceptionHandlingVoidCommandBehavior<TCommand> : ICommandPipelineBehavior<TCommand>
        where TCommand : ICommand
    {
        private readonly ILogger<ExceptionHandlingVoidCommandBehavior<TCommand>> _logger;
        private readonly IExceptionHandler? _exceptionHandler;

        public ExceptionHandlingVoidCommandBehavior(
            ILogger<ExceptionHandlingVoidCommandBehavior<TCommand>> logger,
            IExceptionHandler? exceptionHandler = null)
        {
            _logger = logger;
            _exceptionHandler = exceptionHandler;
        }

        public async Task Handle(
            TCommand command,
            CommandHandlerDelegate next,
            CancellationToken cancellationToken)
        {
            try
            {
                await next();
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
                    "Exception caught while executing void command {CommandName}: {ExceptionMessage}",
                    commandName,
                    ex.Message);

                if (_exceptionHandler != null)
                {
                    var handled = await _exceptionHandler.HandleAsync(
                        ex, commandType, command, cancellationToken);

                    if (handled)
                    {
                        _logger.LogWarning(
                            "Exception in void command {CommandName} was handled",
                            commandName);
                        return;
                    }
                }

                // Rethrow if not handled
                throw;
            }
        }
    }
}
