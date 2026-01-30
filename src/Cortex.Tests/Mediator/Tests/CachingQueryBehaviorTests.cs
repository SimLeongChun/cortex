using Cortex.Mediator.Behaviors;
using Cortex.Mediator.Caching;
using Cortex.Mediator.Queries;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Cortex.Tests.Mediator.Tests
{
    public class CachingQueryBehaviorTests
    {
        #region Test Queries

        // Non-cacheable query (no attribute, doesn't implement ICacheableQuery)
        public class NonCacheableQuery : IQuery<string>
        {
            public string Input { get; set; } = string.Empty;
        }

        // Cacheable query using attribute
        [Cacheable(AbsoluteExpirationSeconds = 120, SlidingExpirationSeconds = 30)]
        public class CacheableAttributeQuery : IQuery<string>
        {
            public string Input { get; set; } = string.Empty;
        }

        // Cacheable query using interface
        public class CacheableInterfaceQuery : IQuery<string>, ICacheableQuery
        {
            public string Input { get; set; } = string.Empty;
            public string? CacheKey => $"custom-{Input}";
            public TimeSpan? AbsoluteExpiration => TimeSpan.FromMinutes(10);
            public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);
        }

        // Cacheable query with custom attribute prefix
        [Cacheable(CacheKeyPrefix = "MyApp")]
        public class CustomPrefixQuery : IQuery<string>
        {
            public string Input { get; set; } = string.Empty;
        }

        #endregion

        private readonly IMemoryCache _cache;
        private readonly Mock<ILogger<CachingQueryBehavior<CacheableAttributeQuery, string>>> _loggerMock;
        private readonly ICacheKeyGenerator _cacheKeyGenerator;
        private readonly IOptions<CachingOptions> _options;

        public CachingQueryBehaviorTests()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            _loggerMock = new Mock<ILogger<CachingQueryBehavior<CacheableAttributeQuery, string>>>();
            _options = Options.Create(new CachingOptions());
            _cacheKeyGenerator = new DefaultCacheKeyGenerator(Options.Create(new CachingOptions()));
        }

        [Fact]
        public async Task Handle_NonCacheableQuery_ShouldNotCache()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<CachingQueryBehavior<NonCacheableQuery, string>>>();
            var behavior = new CachingQueryBehavior<NonCacheableQuery, string>(
                _cache, _cacheKeyGenerator, loggerMock.Object, _options);

            var query = new NonCacheableQuery { Input = "test" };
            var executionCount = 0;

            QueryHandlerDelegate<string> next = () =>
            {
                executionCount++;
                return Task.FromResult("result");
            };

            // Act
            var result1 = await behavior.Handle(query, next, CancellationToken.None);
            var result2 = await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.Equal("result", result1);
            Assert.Equal("result", result2);
            Assert.Equal(2, executionCount); // Should execute twice, no caching
        }

        [Fact]
        public async Task Handle_CacheableQuery_ShouldCacheResult()
        {
            // Arrange
            var behavior = new CachingQueryBehavior<CacheableAttributeQuery, string>(
                _cache, _cacheKeyGenerator, _loggerMock.Object, _options);

            var query = new CacheableAttributeQuery { Input = "test" };
            var executionCount = 0;

            QueryHandlerDelegate<string> next = () =>
            {
                executionCount++;
                return Task.FromResult("result");
            };

            // Act
            var result1 = await behavior.Handle(query, next, CancellationToken.None);
            var result2 = await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.Equal("result", result1);
            Assert.Equal("result", result2);
            Assert.Equal(1, executionCount); // Should execute only once due to caching
        }

        [Fact]
        public async Task Handle_DifferentQueryInputs_ShouldCacheSeparately()
        {
            // Arrange
            var behavior = new CachingQueryBehavior<CacheableAttributeQuery, string>(
                _cache, _cacheKeyGenerator, _loggerMock.Object, _options);

            var query1 = new CacheableAttributeQuery { Input = "test1" };
            var query2 = new CacheableAttributeQuery { Input = "test2" };
            var executionCount = 0;

            QueryHandlerDelegate<string> next = () =>
            {
                executionCount++;
                return Task.FromResult($"result-{executionCount}");
            };

            // Act
            var result1 = await behavior.Handle(query1, next, CancellationToken.None);
            var result2 = await behavior.Handle(query2, next, CancellationToken.None);
            var result1Again = await behavior.Handle(query1, next, CancellationToken.None);

            // Assert
            Assert.Equal("result-1", result1);
            Assert.Equal("result-2", result2);
            Assert.Equal("result-1", result1Again); // Should return cached value
            Assert.Equal(2, executionCount); // Should execute twice (once per unique query)
        }

        [Fact]
        public async Task Handle_CacheableInterface_ShouldUseCustomCacheKey()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<CachingQueryBehavior<CacheableInterfaceQuery, string>>>();
            var behavior = new CachingQueryBehavior<CacheableInterfaceQuery, string>(
                _cache, _cacheKeyGenerator, loggerMock.Object, _options);

            var query = new CacheableInterfaceQuery { Input = "test" };
            var executionCount = 0;

            QueryHandlerDelegate<string> next = () =>
            {
                executionCount++;
                return Task.FromResult("result");
            };

            // Act
            var result1 = await behavior.Handle(query, next, CancellationToken.None);
            var result2 = await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.Equal("result", result1);
            Assert.Equal("result", result2);
            Assert.Equal(1, executionCount); // Should cache using custom key
        }

        [Fact]
        public async Task Handle_WhenCachingDisabled_ShouldNotCache()
        {
            // Arrange
            var disabledOptions = Options.Create(new CachingOptions { EnableCaching = false });
            var behavior = new CachingQueryBehavior<CacheableAttributeQuery, string>(
                _cache, _cacheKeyGenerator, _loggerMock.Object, disabledOptions);

            var query = new CacheableAttributeQuery { Input = "test" };
            var executionCount = 0;

            QueryHandlerDelegate<string> next = () =>
            {
                executionCount++;
                return Task.FromResult("result");
            };

            // Act
            var result1 = await behavior.Handle(query, next, CancellationToken.None);
            var result2 = await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.Equal("result", result1);
            Assert.Equal("result", result2);
            Assert.Equal(2, executionCount); // Should execute twice when caching is disabled
        }

        [Fact]
        public async Task Handle_NullResult_ShouldNotCache()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<CachingQueryBehavior<CacheableAttributeQuery, string?>>>();
            var behavior = new CachingQueryBehavior<CacheableAttributeQuery, string?>(
                _cache, _cacheKeyGenerator, loggerMock.Object, _options);

            var query = new CacheableAttributeQuery { Input = "test" };
            var executionCount = 0;

            QueryHandlerDelegate<string?> next = () =>
            {
                executionCount++;
                return Task.FromResult<string?>(null);
            };

            // Act
            var result1 = await behavior.Handle(query, next, CancellationToken.None);
            var result2 = await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.Null(result1);
            Assert.Null(result2);
            Assert.Equal(2, executionCount); // Should execute twice since null is not cached
        }

        [Fact]
        public async Task Handle_CacheHit_ShouldLogDebug()
        {
            // Arrange
            var behavior = new CachingQueryBehavior<CacheableAttributeQuery, string>(
                _cache, _cacheKeyGenerator, _loggerMock.Object, _options);

            var query = new CacheableAttributeQuery { Input = "test" };
            QueryHandlerDelegate<string> next = () => Task.FromResult("result");

            // Act
            await behavior.Handle(query, next, CancellationToken.None);
            await behavior.Handle(query, next, CancellationToken.None);

            // Assert - verify cache hit was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache hit")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_CacheMiss_ShouldLogDebug()
        {
            // Arrange
            var behavior = new CachingQueryBehavior<CacheableAttributeQuery, string>(
                _cache, _cacheKeyGenerator, _loggerMock.Object, _options);

            var query = new CacheableAttributeQuery { Input = "test" };
            QueryHandlerDelegate<string> next = () => Task.FromResult("result");

            // Act
            await behavior.Handle(query, next, CancellationToken.None);

            // Assert - verify cache miss was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache miss")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
