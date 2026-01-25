using Cortex.Mediator.Commands;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Processors
{
    /// <summary>
    /// Pipeline behavior that executes all registered post-processors after the command handler.
    /// </summary>
    /// <typeparam name="TCommand">The type of command being handled.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the command.</typeparam>
    public sealed class RequestPostProcessorBehavior<TCommand, TResult> : ICommandPipelineBehavior<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        private readonly IEnumerable<IRequestPostProcessor<TCommand, TResult>> _postProcessors;

        public RequestPostProcessorBehavior(IEnumerable<IRequestPostProcessor<TCommand, TResult>> postProcessors)
        {
            _postProcessors = postProcessors;
        }

        public async Task<TResult> Handle(
            TCommand command,
            CommandHandlerDelegate<TResult> next,
            CancellationToken cancellationToken)
        {
            var response = await next();

            foreach (var processor in _postProcessors)
            {
                await processor.ProcessAsync(command, response, cancellationToken);
            }

            return response;
        }
    }

    /// <summary>
    /// Pipeline behavior that executes all registered post-processors after the void command handler.
    /// </summary>
    /// <typeparam name="TCommand">The type of command being handled.</typeparam>
    public sealed class RequestPostProcessorBehavior<TCommand> : ICommandPipelineBehavior<TCommand>
        where TCommand : ICommand
    {
        private readonly IEnumerable<IRequestPostProcessor<TCommand>> _postProcessors;

        public RequestPostProcessorBehavior(IEnumerable<IRequestPostProcessor<TCommand>> postProcessors)
        {
            _postProcessors = postProcessors;
        }

        public async Task Handle(
            TCommand command,
            CommandHandlerDelegate next,
            CancellationToken cancellationToken)
        {
            await next();

            foreach (var processor in _postProcessors)
            {
                await processor.ProcessAsync(command, cancellationToken);
            }
        }
    }
}
