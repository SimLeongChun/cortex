using Cortex.Mediator.Behaviors;

namespace Cortex.Mediator.DependencyInjection
{
    public static class MediatorOptionsExtensions
    {
        /// <summary>
        /// Adds default logging behaviors for commands, queries, and notifications.
        /// </summary>
        public static MediatorOptions AddDefaultBehaviors(this MediatorOptions options)
        {
            return options
                // Register the open generic logging behavior for commands that return TResult
                .AddOpenCommandPipelineBehavior(typeof(LoggingCommandBehavior<,>))
                .AddOpenQueryPipelineBehavior(typeof(LoggingQueryBehavior<,>))
                .AddOpenCommandPipelineBehavior(typeof(LoggingCommandBehavior<>)) // Add void command logging
                .AddOpenNotificationPipelineBehavior(typeof(LoggingNotificationBehavior<>)); // Add notification logging
        }

        /// <summary>
        /// Adds exception handling behaviors for commands, queries, and notifications.
        /// Exception handlers can be registered separately in the DI container.
        /// </summary>
        public static MediatorOptions AddExceptionHandlingBehaviors(this MediatorOptions options)
        {
            return options
                .AddOpenCommandPipelineBehavior(typeof(ExceptionHandlingCommandBehavior<,>))
                .AddOpenCommandPipelineBehavior(typeof(ExceptionHandlingVoidCommandBehavior<>))
                .AddOpenQueryPipelineBehavior(typeof(ExceptionHandlingQueryBehavior<,>))
                .AddOpenNotificationPipelineBehavior(typeof(ExceptionHandlingNotificationBehavior<>));
        }

        /// <summary>
        /// Adds caching behavior for queries.
        /// Queries must implement ICacheableQuery or be decorated with [Cacheable] attribute.
        /// </summary>
        public static MediatorOptions AddCachingBehavior(this MediatorOptions options)
        {
            return options
                .AddOpenQueryPipelineBehavior(typeof(CachingQueryBehavior<,>));
        }

        /// <summary>
        /// Adds both logging and exception handling behaviors.
        /// Exception handling behaviors are registered first so they wrap the logging behaviors.
        /// </summary>
        public static MediatorOptions AddDefaultBehaviorsWithExceptionHandling(this MediatorOptions options)
        {
            return options
                .AddExceptionHandlingBehaviors()
                .AddDefaultBehaviors();
        }

        /// <summary>
        /// Adds all default behaviors including logging, exception handling, and caching.
        /// </summary>
        public static MediatorOptions AddAllBehaviors(this MediatorOptions options)
        {
            return options
                .AddExceptionHandlingBehaviors()
                .AddCachingBehavior()
                .AddDefaultBehaviors();
        }
    }
}
