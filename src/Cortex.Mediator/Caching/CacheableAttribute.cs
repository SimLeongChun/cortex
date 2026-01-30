using System;

namespace Cortex.Mediator.Caching
{
    /// <summary>
    /// Attribute to mark a query class as cacheable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class CacheableAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the absolute expiration time in seconds.
        /// Default is 300 seconds (5 minutes).
        /// </summary>
        public int AbsoluteExpirationSeconds { get; set; } = 300;

        /// <summary>
        /// Gets or sets the sliding expiration time in seconds.
        /// Default is 60 seconds (1 minute).
        /// </summary>
        public int SlidingExpirationSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets a custom cache key prefix.
        /// If not set, the query type name will be used as the prefix.
        /// </summary>
        public string? CacheKeyPrefix { get; set; }

        /// <summary>
        /// Gets the absolute expiration as a TimeSpan.
        /// </summary>
        public TimeSpan AbsoluteExpiration => TimeSpan.FromSeconds(AbsoluteExpirationSeconds);

        /// <summary>
        /// Gets the sliding expiration as a TimeSpan.
        /// </summary>
        public TimeSpan SlidingExpiration => TimeSpan.FromSeconds(SlidingExpirationSeconds);
    }
}
