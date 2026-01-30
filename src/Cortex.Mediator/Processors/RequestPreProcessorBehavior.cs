using Cortex.Mediator.Commands;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Processors
{
    /// <summary>
    /// Pipeline behavior that executes all registered pre-processors before the command handler.
    /// </summary>
    /// <typeparam name="TCommand">The type of command being handled.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the command.</typeparam>
    public sealed class RequestPreProcessorBehavior<TCommand, TResult> : ICommandPipelineBehavior<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        private readonly IEnumerable<IRequestPreProcessor<TCommand>> _preProcessors;

        public RequestPreProcessorBehavior(IEnumerable<IRequestPreProcessor<TCommand>> preProcessors)
        {
            _preProcessors = preProcessors;
        }

        public async Task<TResult> Handle(
            TCommand command,
            CommandHandlerDelegate<TResult> next,
            CancellationToken cancellationToken)
        {
            foreach (var processor in _preProcessors)
            {
                await processor.ProcessAsync(command, cancellationToken);
            }

            return await next();
        }
    }

    /// <summary>
    /// Pipeline behavior that executes all registered pre-processors before the void command handler.
    /// </summary>
    /// <typeparam name="TCommand">The type of command being handled.</typeparam>
    public sealed class RequestPreProcessorBehavior<TCommand> : ICommandPipelineBehavior<TCommand>
        where TCommand : ICommand
    {
        private readonly IEnumerable<IRequestPreProcessor<TCommand>> _preProcessors;

        public RequestPreProcessorBehavior(IEnumerable<IRequestPreProcessor<TCommand>> preProcessors)
        {
            _preProcessors = preProcessors;
        }

        public async Task Handle(
            TCommand command,
            CommandHandlerDelegate next,
            CancellationToken cancellationToken)
        {
            foreach (var processor in _preProcessors)
            {
                await processor.ProcessAsync(command, cancellationToken);
            }

            await next();
        }
    }
}
