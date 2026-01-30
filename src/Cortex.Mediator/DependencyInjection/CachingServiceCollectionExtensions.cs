using Cortex.Mediator.Caching;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Cortex.Mediator.DependencyInjection
{
    /// <summary>
    /// Extension methods for registering caching services.
    /// </summary>
    public static class CachingServiceCollectionExtensions
    {
        /// <summary>
        /// Adds caching services required for the CachingQueryBehavior.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional action to configure caching options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMediatorCaching(
            this IServiceCollection services,
            Action<CachingOptions>? configure = null)
        {
            // Register caching options
            if (configure != null)
            {
                services.Configure(configure);
            }
            else
            {
                services.Configure<CachingOptions>(_ => { });
            }

            // Add memory cache if not already registered
            services.AddMemoryCache();

            // Register cache key generator
            services.AddSingleton<ICacheKeyGenerator, DefaultCacheKeyGenerator>();

            // Register cache invalidator
            services.AddSingleton<ICacheInvalidator, CacheInvalidator>();

            return services;
        }

        /// <summary>
        /// Adds caching services with custom cache key generator.
        /// </summary>
        /// <typeparam name="TCacheKeyGenerator">The type of cache key generator to use.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional action to configure caching options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMediatorCaching<TCacheKeyGenerator>(
            this IServiceCollection services,
            Action<CachingOptions>? configure = null)
            where TCacheKeyGenerator : class, ICacheKeyGenerator
        {
            // Register caching options
            if (configure != null)
            {
                services.Configure(configure);
            }
            else
            {
                services.Configure<CachingOptions>(_ => { });
            }

            // Add memory cache if not already registered
            services.AddMemoryCache();

            // Register custom cache key generator
            services.AddSingleton<ICacheKeyGenerator, TCacheKeyGenerator>();

            // Register cache invalidator
            services.AddSingleton<ICacheInvalidator, CacheInvalidator>();

            return services;
        }
    }
}
