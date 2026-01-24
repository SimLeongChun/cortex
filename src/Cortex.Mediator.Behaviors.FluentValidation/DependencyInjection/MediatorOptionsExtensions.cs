using Cortex.Mediator.DependencyInjection;

namespace Cortex.Mediator.Behaviors.FluentValidation.DependencyInjection
{
    public static class MediatorOptionsExtensions
    {
        public static MediatorOptions AddFluentValidationBehaviors(this MediatorOptions options)
        {
            options.AddOpenCommandPipelineBehavior(typeof(ValidationCommandBehavior<,>))
                .AddOpenQueryPipelineBehavior(typeof(ValidationQueryBehavior<,>))
                .AddOpenCommandPipelineBehavior(typeof(ValidationCommandBehavior<>));

            return options;
        }
    }
}
