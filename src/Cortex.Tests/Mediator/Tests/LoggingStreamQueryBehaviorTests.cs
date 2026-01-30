using Cortex.Mediator.Behaviors;
using Cortex.Mediator.Streaming;
using Microsoft.Extensions.Logging;
using Moq;
using System.Runtime.CompilerServices;

namespace Cortex.Tests.Mediator.Tests
{
    public class LoggingStreamQueryBehaviorTests
    {
        public class TestStreamQuery : IStreamQuery<string>
        {
            public int Count { get; set; } = 3;
        }

        [Fact]
        public async Task Handle_ShouldLogStartAndCompletion()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<LoggingStreamQueryBehavior<TestStreamQuery, string>>>();
            var behavior = new LoggingStreamQueryBehavior<TestStreamQuery, string>(mockLogger.Object);
            var query = new TestStreamQuery { Count = 3 };

            StreamQueryHandlerDelegate<string> next = () => CreateTestStream(query.Count);

            // Act
            var items = new List<string>();
            await foreach (var item in behavior.Handle(query, next, CancellationToken.None))
            {
                items.Add(item);
            }

            // Assert
            Assert.Equal(3, items.Count);

            // Verify start was logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting stream")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Verify completion was logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed successfully")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldLogEachItemAtDebugLevel()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<LoggingStreamQueryBehavior<TestStreamQuery, string>>>();
            mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);

            var behavior = new LoggingStreamQueryBehavior<TestStreamQuery, string>(mockLogger.Object);
            var query = new TestStreamQuery { Count = 3 };

            StreamQueryHandlerDelegate<string> next = () => CreateTestStream(query.Count);

            // Act
            await foreach (var _ in behavior.Handle(query, next, CancellationToken.None))
            {
                // Consume all items
            }

            // Assert - verify debug logs for items
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("yielded item")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(3));
        }

        [Fact]
        public async Task Handle_WhenStreamCreationThrows_ShouldLogError()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<LoggingStreamQueryBehavior<TestStreamQuery, string>>>();
            var behavior = new LoggingStreamQueryBehavior<TestStreamQuery, string>(mockLogger.Object);
            var query = new TestStreamQuery();
            var expectedException = new InvalidOperationException("Stream creation failed");

            StreamQueryHandlerDelegate<string> next = () => throw expectedException;

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (var _ in behavior.Handle(query, next, CancellationToken.None))
                {
                }
            });

            // Verify error was logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error creating stream")),
                    expectedException,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldPassThroughAllItems()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<LoggingStreamQueryBehavior<TestStreamQuery, string>>>();
            var behavior = new LoggingStreamQueryBehavior<TestStreamQuery, string>(mockLogger.Object);
            var query = new TestStreamQuery { Count = 5 };

            StreamQueryHandlerDelegate<string> next = () => CreateTestStream(query.Count);

            // Act
            var items = new List<string>();
            await foreach (var item in behavior.Handle(query, next, CancellationToken.None))
            {
                items.Add(item);
            }

            // Assert
            Assert.Equal(5, items.Count);
            Assert.Equal("Item-1", items[0]);
            Assert.Equal("Item-5", items[4]);
        }

        [Fact]
        public async Task Handle_WithCancellation_ShouldRespectCancellation()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<LoggingStreamQueryBehavior<TestStreamQuery, string>>>();
            var behavior = new LoggingStreamQueryBehavior<TestStreamQuery, string>(mockLogger.Object);
            var query = new TestStreamQuery { Count = 100 };
            var cts = new CancellationTokenSource();

            StreamQueryHandlerDelegate<string> next = () => CreateTestStreamWithCancellation(query.Count, cts.Token);

            // Act
            var items = new List<string>();
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var item in behavior.Handle(query, next, cts.Token))
                {
                    items.Add(item);
                    if (items.Count >= 3)
                    {
                        cts.Cancel();
                    }
                }
            });

            // Assert
            Assert.Equal(3, items.Count);
        }

        private static async IAsyncEnumerable<string> CreateTestStream(int count)
        {
            for (int i = 1; i <= count; i++)
            {
                await Task.Delay(1);
                yield return $"Item-{i}";
            }
        }

        private static async IAsyncEnumerable<string> CreateTestStreamWithCancellation(
            int count,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 1; i <= count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(1, cancellationToken);
                yield return $"Item-{i}";
            }
        }
    }
}
