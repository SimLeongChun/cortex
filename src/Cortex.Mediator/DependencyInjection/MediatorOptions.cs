using Cortex.Mediator.Commands;
using Cortex.Mediator.Queries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cortex.Mediator.DependencyInjection
{
    public class MediatorOptions
    {
        internal List<Type> CommandBehaviors { get; } = new();
        internal List<Type> VoidCommandBehaviors { get; } = new();
        internal List<Type> QueryBehaviors { get; } = new();

        public bool OnlyPublicClasses { get; set; } = true;


        /// <summary>
        /// Register a *closed* command pipeline behavior.
        /// </summary>
        public MediatorOptions AddCommandPipelineBehavior<TBehavior>()
            where TBehavior : class // Add constraint
        {
            var behaviorType = typeof(TBehavior);

            if (behaviorType.IsGenericTypeDefinition)
                throw new ArgumentException("Open generic types must be registered using AddOpenCommandPipelineBehavior");

            var implementsReturning =
                behaviorType.GetInterfaces().Any(i => i.IsGenericType &&
                                                      i.GetGenericTypeDefinition() == typeof(ICommandPipelineBehavior<,>));

            var implementsNonReturning =
                behaviorType.GetInterfaces().Any(i => i.IsGenericType &&
                                                      i.GetGenericTypeDefinition() == typeof(ICommandPipelineBehavior<>));

            if (!implementsReturning && !implementsNonReturning)
                throw new ArgumentException("Type must implement ICommandPipelineBehavior<,> or ICommandPipelineBehavior<>");

            if (implementsReturning)
                CommandBehaviors.Add(behaviorType);

            if (implementsNonReturning)
                VoidCommandBehaviors.Add(behaviorType);

            return this;
        }

        /// <summary>
        /// Register an *open generic* command pipeline behavior, e.g. typeof(LoggingCommandBehavior&lt;,&gt;).
        /// </summary>
        public MediatorOptions AddOpenCommandPipelineBehavior(Type openGenericBehaviorType)
        {
            if (!openGenericBehaviorType.IsGenericTypeDefinition)
                throw new ArgumentException("Type must be an open generic type definition");

            var implementsReturning =
                openGenericBehaviorType.GetInterfaces().Any(i => i.IsGenericType &&
                                                                 i.GetGenericTypeDefinition() == typeof(ICommandPipelineBehavior<,>));

            var implementsNonReturning =
                openGenericBehaviorType.GetInterfaces().Any(i => i.IsGenericType &&
                                                                 i.GetGenericTypeDefinition() == typeof(ICommandPipelineBehavior<>));

            if (!implementsReturning && !implementsNonReturning)
                throw new ArgumentException("Type must implement ICommandPipelineBehavior<,> or ICommandPipelineBehavior<>");

            if (implementsReturning)
                CommandBehaviors.Add(openGenericBehaviorType);

            if (implementsNonReturning)
                VoidCommandBehaviors.Add(openGenericBehaviorType);

            return this;
        }

        public MediatorOptions AddOpenQueryPipelineBehavior(Type openGenericBehaviorType)
        {
            if (!openGenericBehaviorType.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Type must be an open generic type definition");
            }

            var queryBehaviorInterface = openGenericBehaviorType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                                   i.GetGenericTypeDefinition() == typeof(IQueryPipelineBehavior<,>));

            if (queryBehaviorInterface == null)
            {
                throw new ArgumentException("Type must implement IQueryPipelineBehavior<,>");
            }

            QueryBehaviors.Add(openGenericBehaviorType);
            return this;
        }
    }
}
