using Cortex.Mediator.Caching;
using Cortex.Mediator.Queries;
using Microsoft.Extensions.Options;

namespace Cortex.Tests.Mediator.Tests
{
    public class DefaultCacheKeyGeneratorTests
    {
        public class TestQuery : IQuery<string>
        {
            public string Input { get; set; } = string.Empty;
            public int Number { get; set; }
        }

        [Cacheable(CacheKeyPrefix = "CustomPrefix")]
        public class CustomPrefixQuery : IQuery<string>
        {
            public string Input { get; set; } = string.Empty;
        }

        public class CacheableQuery : IQuery<string>, ICacheableQuery
        {
            public string Input { get; set; } = string.Empty;
            public string? CacheKey => $"my-custom-key-{Input}";
            public TimeSpan? AbsoluteExpiration => null;
            public TimeSpan? SlidingExpiration => null;
        }

        private readonly DefaultCacheKeyGenerator _generator;

        public DefaultCacheKeyGeneratorTests()
        {
            _generator = new DefaultCacheKeyGenerator(Options.Create(new CachingOptions
            {
                CacheKeyPrefix = "TestPrefix",
                IncludeQueryPropertiesInCacheKey = true
            }));
        }

        [Fact]
        public void GenerateKey_ShouldIncludePrefix()
        {
            // Arrange
            var query = new TestQuery { Input = "test", Number = 42 };

            // Act
            var key = _generator.GenerateKey<TestQuery, string>(query);

            // Assert
            Assert.StartsWith("TestPrefix:", key);
        }

        [Fact]
        public void GenerateKey_ShouldIncludeQueryTypeName()
        {
            // Arrange
            var query = new TestQuery { Input = "test", Number = 42 };

            // Act
            var key = _generator.GenerateKey<TestQuery, string>(query);

            // Assert
            Assert.Contains("TestQuery", key);
        }

        [Fact]
        public void GenerateKey_SameQuery_ShouldReturnSameKey()
        {
            // Arrange
            var query1 = new TestQuery { Input = "test", Number = 42 };
            var query2 = new TestQuery { Input = "test", Number = 42 };

            // Act
            var key1 = _generator.GenerateKey<TestQuery, string>(query1);
            var key2 = _generator.GenerateKey<TestQuery, string>(query2);

            // Assert
            Assert.Equal(key1, key2);
        }

        [Fact]
        public void GenerateKey_DifferentQueries_ShouldReturnDifferentKeys()
        {
            // Arrange
            var query1 = new TestQuery { Input = "test1", Number = 42 };
            var query2 = new TestQuery { Input = "test2", Number = 42 };

            // Act
            var key1 = _generator.GenerateKey<TestQuery, string>(query1);
            var key2 = _generator.GenerateKey<TestQuery, string>(query2);

            // Assert
            Assert.NotEqual(key1, key2);
        }

        [Fact]
        public void GenerateKey_CustomPrefixAttribute_ShouldUseCustomPrefix()
        {
            // Arrange
            var query = new CustomPrefixQuery { Input = "test" };

            // Act
            var key = _generator.GenerateKey<CustomPrefixQuery, string>(query);

            // Assert
            Assert.StartsWith("CustomPrefix:", key);
        }

        [Fact]
        public void GenerateKey_ICacheableQuery_ShouldUseCustomCacheKey()
        {
            // Arrange
            var query = new CacheableQuery { Input = "test" };

            // Act
            var key = _generator.GenerateKey<CacheableQuery, string>(query);

            // Assert
            Assert.Contains("my-custom-key-test", key);
        }

        [Fact]
        public void GenerateKey_NullQuery_ShouldThrow()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _generator.GenerateKey<TestQuery, string>(null!));
        }

        [Fact]
        public void GenerateKey_WithoutProperties_ShouldStillWork()
        {
            // Arrange
            var generatorWithoutProps = new DefaultCacheKeyGenerator(Options.Create(new CachingOptions
            {
                CacheKeyPrefix = "Test",
                IncludeQueryPropertiesInCacheKey = false
            }));
            var query = new TestQuery { Input = "test", Number = 42 };

            // Act
            var key = generatorWithoutProps.GenerateKey<TestQuery, string>(query);

            // Assert
            Assert.Equal("Test:TestQuery", key);
        }
    }
}
