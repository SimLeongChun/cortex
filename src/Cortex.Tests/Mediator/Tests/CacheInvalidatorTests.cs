using Cortex.Mediator.Caching;
using Cortex.Mediator.Queries;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cortex.Tests.Mediator.Tests
{
    public class CacheInvalidatorTests
    {
        public class TestQuery : IQuery<string>
        {
            public string Input { get; set; } = string.Empty;
        }

        private readonly IMemoryCache _cache;
        private readonly ICacheKeyGenerator _keyGenerator;
        private readonly CacheInvalidator _invalidator;

        public CacheInvalidatorTests()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            _keyGenerator = new DefaultCacheKeyGenerator(Options.Create(new CachingOptions
            {
                CacheKeyPrefix = "Test"
            }));
            _invalidator = new CacheInvalidator(_cache, _keyGenerator);
        }

        [Fact]
        public void Invalidate_ShouldRemoveCachedItem()
        {
            // Arrange
            var query = new TestQuery { Input = "test" };
            var cacheKey = _keyGenerator.GenerateKey<TestQuery, string>(query);
            _cache.Set(cacheKey, "cached-value");

            // Verify item is in cache
            Assert.True(_cache.TryGetValue(cacheKey, out _));

            // Act
            _invalidator.Invalidate<TestQuery, string>(query);

            // Assert
            Assert.False(_cache.TryGetValue(cacheKey, out _));
        }

        [Fact]
        public void InvalidateByKey_ShouldRemoveCachedItem()
        {
            // Arrange
            var cacheKey = "Test:MyKey";
            _cache.Set(cacheKey, "cached-value");

            // Verify item is in cache
            Assert.True(_cache.TryGetValue(cacheKey, out _));

            // Act
            _invalidator.InvalidateByKey(cacheKey);

            // Assert
            Assert.False(_cache.TryGetValue(cacheKey, out _));
        }

        [Fact]
        public void InvalidateByKey_EmptyKey_ShouldThrow()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _invalidator.InvalidateByKey(""));
            Assert.Throws<ArgumentException>(() => _invalidator.InvalidateByKey(null!));
        }

        [Fact]
        public void Invalidate_NonExistentKey_ShouldNotThrow()
        {
            // Arrange
            var query = new TestQuery { Input = "non-existent" };

            // Act & Assert - should not throw
            _invalidator.Invalidate<TestQuery, string>(query);
        }
    }
}
