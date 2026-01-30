using Cortex.Mediator;
using Cortex.Mediator.Behaviors;
using Cortex.Mediator.DependencyInjection;
using Cortex.Mediator.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;

namespace Cortex.Tests.Mediator.Tests
{
    public class StreamingIntegrationTests
    {
        #region Test Streaming Queries and Handlers

        public class GetProductsStreamQuery : IStreamQuery<ProductItem>
        {
            public int PageSize { get; set; } = 10;
        }

        public class ProductItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
        }

        public class GetProductsStreamQueryHandler : IStreamQueryHandler<GetProductsStreamQuery, ProductItem>
        {
            public async IAsyncEnumerable<ProductItem> Handle(
                GetProductsStreamQuery query,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                for (int i = 1; i <= query.PageSize; i++)
                {
                    await Task.Delay(1, cancellationToken);
                    yield return new ProductItem
                    {
                        Id = i,
                        Name = $"Product-{i}",
                        Price = i * 10.00m
                    };
                }
            }
        }

        public class TestStreamQueryBehavior<TQuery, TResult> : IStreamQueryPipelineBehavior<TQuery, TResult>
            where TQuery : IStreamQuery<TResult>
        {
            private readonly List<string> _log;

            public TestStreamQueryBehavior(List<string> log)
            {
                _log = log;
            }

            public IAsyncEnumerable<TResult> Handle(
                TQuery query,
                StreamQueryHandlerDelegate<TResult> next,
                CancellationToken cancellationToken)
            {
                return ExecuteWithLogging(query, next, cancellationToken);
            }

            private async IAsyncEnumerable<TResult> ExecuteWithLogging(
                TQuery query,
                StreamQueryHandlerDelegate<TResult> next,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                _log.Add("Before Stream");

                await foreach (var item in next().WithCancellation(cancellationToken))
                {
                    _log.Add($"Item yielded");
                    yield return item;
                }

                _log.Add("After Stream");
            }
        }

        #endregion

        [Fact]
        public async Task StreamQuery_WithDI_ShouldWork()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

            services.AddCortexMediator(
                Array.Empty<Type>(),
                options => { });

            services.AddTransient<IStreamQueryHandler<GetProductsStreamQuery, ProductItem>, GetProductsStreamQueryHandler>();

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var items = new List<ProductItem>();
            await foreach (var item in mediator.StreamAsync(new GetProductsStreamQuery { PageSize = 5 }))
            {
                items.Add(item);
            }

            // Assert
            Assert.Equal(5, items.Count);
            Assert.Equal("Product-1", items[0].Name);
            Assert.Equal(10.00m, items[0].Price);
        }

        [Fact]
        public async Task StreamQuery_WithPipelineBehavior_ShouldExecuteBehavior()
        {
            // Arrange
            var log = new List<string>();
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton(log);

            services.AddCortexMediator(
                Array.Empty<Type>(),
                options => options.AddOpenStreamQueryPipelineBehavior(typeof(TestStreamQueryBehavior<,>)));

            services.AddTransient<IStreamQueryHandler<GetProductsStreamQuery, ProductItem>, GetProductsStreamQueryHandler>();

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var items = new List<ProductItem>();
            await foreach (var item in mediator.StreamAsync(new GetProductsStreamQuery { PageSize = 3 }))
            {
                items.Add(item);
            }

            // Assert
            Assert.Equal(3, items.Count);
            Assert.Equal(5, log.Count); // Before + 3 items + After
            Assert.Equal("Before Stream", log[0]);
            Assert.Equal("Item yielded", log[1]);
            Assert.Equal("Item yielded", log[2]);
            Assert.Equal("Item yielded", log[3]);
            Assert.Equal("After Stream", log[4]);
        }

        [Fact]
        public async Task StreamQuery_WithLoggingBehavior_ShouldLogCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

            services.AddCortexMediator(
                Array.Empty<Type>(),
                options => options.AddOpenStreamQueryPipelineBehavior(typeof(LoggingStreamQueryBehavior<,>)));

            services.AddTransient<IStreamQueryHandler<GetProductsStreamQuery, ProductItem>, GetProductsStreamQueryHandler>();

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act - should not throw
            var items = new List<ProductItem>();
            await foreach (var item in mediator.StreamAsync(new GetProductsStreamQuery { PageSize = 5 }))
            {
                items.Add(item);
            }

            // Assert
            Assert.Equal(5, items.Count);
        }

        [Fact]
        public async Task StreamQuery_CanBeConsumedMultipleTimes()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

            services.AddCortexMediator(Array.Empty<Type>(), _ => { });
            services.AddTransient<IStreamQueryHandler<GetProductsStreamQuery, ProductItem>, GetProductsStreamQueryHandler>();

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var query = new GetProductsStreamQuery { PageSize = 3 };
            
            var items1 = new List<ProductItem>();
            await foreach (var item in mediator.StreamAsync(query))
            {
                items1.Add(item);
            }

            var items2 = new List<ProductItem>();
            await foreach (var item in mediator.StreamAsync(query))
            {
                items2.Add(item);
            }

            // Assert
            Assert.Equal(3, items1.Count);
            Assert.Equal(3, items2.Count);
        }

        [Fact]
        public async Task StreamQuery_ProcessItemsAsTheyArrive()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

            services.AddCortexMediator(Array.Empty<Type>(), _ => { });
            services.AddTransient<IStreamQueryHandler<GetProductsStreamQuery, ProductItem>, GetProductsStreamQueryHandler>();

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act - process items one by one
            var processedIds = new List<int>();
            await foreach (var item in mediator.StreamAsync(new GetProductsStreamQuery { PageSize = 5 }))
            {
                processedIds.Add(item.Id);
                // Simulate processing each item
                await Task.Delay(1);
            }

            // Assert - items should arrive in order
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, processedIds);
        }
    }
}
