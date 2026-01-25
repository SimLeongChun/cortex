using Cortex.Mediator.Behaviors;
using Cortex.Mediator.Queries;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cortex.Tests.Mediator.Tests
{
    public class ExceptionHandlingQueryBehaviorTests
    {
        public class TestQuery : IQuery<string>
        {
            public string Input { get; set; } = string.Empty;
        }

        [Fact]
        public async Task Handle_WhenNoException_ShouldReturnResult()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingQueryBehavior<TestQuery, string>>>();
            var behavior = new ExceptionHandlingQueryBehavior<TestQuery, string>(mockLogger.Object);
            var query = new TestQuery { Input = "test" };
            var expectedResult = "success";

            QueryHandlerDelegate<string> next = () => Task.FromResult(expectedResult);

            // Act
            var result = await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task Handle_WhenExceptionThrownAndNoHandler_ShouldLogAndRethrow()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingQueryBehavior<TestQuery, string>>>();
            var behavior = new ExceptionHandlingQueryBehavior<TestQuery, string>(mockLogger.Object);
            var query = new TestQuery { Input = "test" };
            var expectedException = new InvalidOperationException("Test exception");

            QueryHandlerDelegate<string> next = () => throw expectedException;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await behavior.Handle(query, next, CancellationToken.None));

            Assert.Equal("Test exception", exception.Message);

            // Verify error was logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Exception caught")),
                    expectedException,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_WhenExceptionHandledWithResult_ShouldReturnFallbackResult()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingQueryBehavior<TestQuery, string>>>();
            var mockHandler = new Mock<IExceptionHandler<string>>();
            var fallbackResult = "fallback";

            mockHandler.Setup(h => h.HandleWithResultAsync(
                    It.IsAny<Exception>(),
                    It.IsAny<Type>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((true, fallbackResult));

            var behavior = new ExceptionHandlingQueryBehavior<TestQuery, string>(
                mockLogger.Object,
                exceptionHandlerWithResult: mockHandler.Object);

            var query = new TestQuery { Input = "test" };
            QueryHandlerDelegate<string> next = () => throw new InvalidOperationException("Test");

            // Act
            var result = await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.Equal(fallbackResult, result);
            mockHandler.Verify(h => h.HandleWithResultAsync(
                It.IsAny<Exception>(),
                typeof(TestQuery),
                query,
                CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Handle_WhenExceptionHandledWithoutResult_ShouldReturnDefault()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingQueryBehavior<TestQuery, string>>>();
            var mockHandler = new Mock<IExceptionHandler>();

            mockHandler.Setup(h => h.HandleAsync(
                    It.IsAny<Exception>(),
                    It.IsAny<Type>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var behavior = new ExceptionHandlingQueryBehavior<TestQuery, string>(
                mockLogger.Object,
                exceptionHandler: mockHandler.Object);

            var query = new TestQuery { Input = "test" };
            QueryHandlerDelegate<string> next = () => throw new InvalidOperationException("Test");

            // Act
            var result = await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.Null(result); // default for string is null
        }

        [Fact]
        public async Task Handle_WhenCancellationRequested_ShouldRethrowWithoutHandling()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingQueryBehavior<TestQuery, string>>>();
            var mockHandler = new Mock<IExceptionHandler>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var behavior = new ExceptionHandlingQueryBehavior<TestQuery, string>(
                mockLogger.Object,
                exceptionHandler: mockHandler.Object);

            var query = new TestQuery { Input = "test" };
            QueryHandlerDelegate<string> next = () => throw new OperationCanceledException(cts.Token);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await behavior.Handle(query, next, cts.Token));

            // Verify handler was NOT called for cancellation
            mockHandler.Verify(h => h.HandleAsync(
                It.IsAny<Exception>(),
                It.IsAny<Type>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_WhenHandlerReturnsFalse_ShouldRethrowException()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingQueryBehavior<TestQuery, string>>>();
            var mockHandler = new Mock<IExceptionHandler<string>>();

            mockHandler.Setup(h => h.HandleWithResultAsync(
                    It.IsAny<Exception>(),
                    It.IsAny<Type>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((false, (string?)null));

            var behavior = new ExceptionHandlingQueryBehavior<TestQuery, string>(
                mockLogger.Object,
                exceptionHandlerWithResult: mockHandler.Object);

            var query = new TestQuery { Input = "test" };
            var expectedException = new InvalidOperationException("Test");
            QueryHandlerDelegate<string> next = () => throw expectedException;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await behavior.Handle(query, next, CancellationToken.None));

            Assert.Same(expectedException, exception);
        }
    }
}
