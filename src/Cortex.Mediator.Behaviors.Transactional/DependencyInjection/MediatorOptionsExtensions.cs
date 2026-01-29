using Cortex.Mediator.DependencyInjection;

namespace Cortex.Mediator.Behaviors.Transactional.DependencyInjection
{
    /// <summary>
    /// Extension methods for adding transactional behaviors to the mediator options.
    /// </summary>
    public static class MediatorOptionsExtensions
    {
        /// <summary>
        /// Adds transactional pipeline behaviors for commands.
        /// This will wrap command execution in a transaction scope.
        /// </summary>
        /// <param name="options">The mediator options.</param>
        /// <returns>The mediator options for chaining.</returns>
        public static MediatorOptions AddTransactionalBehaviors(this MediatorOptions options)
        {
            options.AddOpenCommandPipelineBehavior(typeof(TransactionalCommandBehavior<,>))
                   .AddOpenCommandPipelineBehavior(typeof(TransactionalCommandBehavior<>));

            return options;
        }
    }
}
