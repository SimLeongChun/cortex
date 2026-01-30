using System;

namespace Cortex.Mediator.Caching
{
    /// <summary>
    /// Interface to mark a query as cacheable with caching options.
    /// </summary>
    public interface ICacheableQuery
    {
        /// <summary>
        /// Gets the cache key for this query. If null, a default key will be generated.
        /// </summary>
        string? CacheKey { get; }

        /// <summary>
        /// Gets the absolute expiration time for the cached result.
        /// If null, the default expiration from CachingOptions will be used.
        /// </summary>
        TimeSpan? AbsoluteExpiration { get; }

        /// <summary>
        /// Gets the sliding expiration time for the cached result.
        /// If null, the default sliding expiration from CachingOptions will be used.
        /// </summary>
        TimeSpan? SlidingExpiration { get; }
    }
}
