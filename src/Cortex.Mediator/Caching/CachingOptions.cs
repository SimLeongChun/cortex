using System;

namespace Cortex.Mediator.Caching
{
    /// <summary>
    /// Options for configuring the caching behavior.
    /// </summary>
    public class CachingOptions
    {
        /// <summary>
        /// Gets or sets the default absolute expiration time for cached results.
        /// Default is 5 minutes.
        /// </summary>
        public TimeSpan DefaultAbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the default sliding expiration time for cached results.
        /// Default is 1 minute.
        /// </summary>
        public TimeSpan DefaultSlidingExpiration { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the cache key prefix used for all cached queries.
        /// Default is "CortexMediator".
        /// </summary>
        public string CacheKeyPrefix { get; set; } = "CortexMediator";

        /// <summary>
        /// Gets or sets whether to include the query properties in the cache key generation.
        /// Default is true.
        /// </summary>
        public bool IncludeQueryPropertiesInCacheKey { get; set; } = true;

        /// <summary>
        /// Gets or sets whether caching is enabled globally.
        /// Default is true.
        /// </summary>
        public bool EnableCaching { get; set; } = true;
    }
}
