using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace Cortex.Mediator.Behaviors.Transactional.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring transactional services in the dependency injection container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds transactional behavior support to the service collection with default options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddTransactionalBehavior(this IServiceCollection services)
        {
            return services.AddTransactionalBehavior(options => { });
        }

        /// <summary>
        /// Adds transactional behavior support to the service collection with custom options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">Action to configure the transactional options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddTransactionalBehavior(
            this IServiceCollection services,
            Action<TransactionalOptions> configureOptions)
        {
            var options = new TransactionalOptions();
            configureOptions?.Invoke(options);

            services.TryAddSingleton(options);

            return services;
        }

        /// <summary>
        /// Adds a custom transactional context implementation to the service collection.
        /// </summary>
        /// <typeparam name="TContext">The type of transactional context.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddTransactionalContext<TContext>(this IServiceCollection services)
            where TContext : class, ITransactionalContext
        {
            services.TryAddScoped<ITransactionalContext, TContext>();
            return services;
        }

        /// <summary>
        /// Adds a custom transactional context implementation to the service collection with a factory.
        /// </summary>
        /// <typeparam name="TContext">The type of transactional context.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="implementationFactory">The factory that creates the context.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddTransactionalContext<TContext>(
            this IServiceCollection services,
            Func<IServiceProvider, TContext> implementationFactory)
            where TContext : class, ITransactionalContext
        {
            services.TryAddScoped<ITransactionalContext>(implementationFactory);
            return services;
        }
    }
}
