using Cortex.Mediator.Queries;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Cortex.Mediator.Caching
{
    /// <summary>
    /// Interface for invalidating cached query results.
    /// </summary>
    public interface ICacheInvalidator
    {
        /// <summary>
        /// Invalidates the cached result for a specific query.
        /// </summary>
        /// <typeparam name="TQuery">The type of query.</typeparam>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <param name="query">The query whose cached result should be invalidated.</param>
        void Invalidate<TQuery, TResult>(TQuery query) where TQuery : IQuery<TResult>;

        /// <summary>
        /// Invalidates all cached results for a specific query type.
        /// </summary>
        /// <typeparam name="TQuery">The type of query.</typeparam>
        /// <typeparam name="TResult">The type of result.</typeparam>
        void InvalidateAll<TQuery, TResult>() where TQuery : IQuery<TResult>;

        /// <summary>
        /// Invalidates a cached result by its cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key to invalidate.</param>
        void InvalidateByKey(string cacheKey);
    }

    /// <summary>
    /// Default implementation of cache invalidator using IMemoryCache.
    /// </summary>
    public class CacheInvalidator : ICacheInvalidator
    {
        private readonly IMemoryCache _cache;
        private readonly ICacheKeyGenerator _cacheKeyGenerator;
        private readonly ConcurrentDictionary<string, HashSet<string>> _keysByType = new();

        public CacheInvalidator(IMemoryCache cache, ICacheKeyGenerator cacheKeyGenerator)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _cacheKeyGenerator = cacheKeyGenerator ?? throw new ArgumentNullException(nameof(cacheKeyGenerator));
        }

        public void Invalidate<TQuery, TResult>(TQuery query) where TQuery : IQuery<TResult>
        {
            var cacheKey = _cacheKeyGenerator.GenerateKey<TQuery, TResult>(query);
            InvalidateByKey(cacheKey);
        }

        public void InvalidateAll<TQuery, TResult>() where TQuery : IQuery<TResult>
        {
            var typeName = typeof(TQuery).Name;
            if (_keysByType.TryGetValue(typeName, out var keys))
            {
                foreach (var key in keys)
                {
                    _cache.Remove(key);
                }
                keys.Clear();
            }
        }

        public void InvalidateByKey(string cacheKey)
        {
            if (string.IsNullOrEmpty(cacheKey))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(cacheKey));

            _cache.Remove(cacheKey);
        }

        /// <summary>
        /// Tracks a cache key for a specific query type (for bulk invalidation).
        /// This method is called internally by the caching behavior.
        /// </summary>
        internal void TrackKey(string queryTypeName, string cacheKey)
        {
            var keys = _keysByType.GetOrAdd(queryTypeName, _ => new HashSet<string>());
            lock (keys)
            {
                keys.Add(cacheKey);
            }
        }
    }
}
