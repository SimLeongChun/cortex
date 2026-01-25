using Cortex.Mediator.Behaviors;
using Cortex.Mediator.Commands;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cortex.Tests.Mediator.Tests
{
    public class ExceptionHandlingVoidCommandBehaviorTests
    {
        public class TestVoidCommand : ICommand
        {
            public string Input { get; set; } = string.Empty;
        }

        [Fact]
        public async Task Handle_WhenNoException_ShouldComplete()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingVoidCommandBehavior<TestVoidCommand>>>();
            var behavior = new ExceptionHandlingVoidCommandBehavior<TestVoidCommand>(mockLogger.Object);
            var command = new TestVoidCommand { Input = "test" };
            var executed = false;

            CommandHandlerDelegate next = () =>
            {
                executed = true;
                return Task.CompletedTask;
            };

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public async Task Handle_WhenExceptionThrownAndNoHandler_ShouldLogAndRethrow()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingVoidCommandBehavior<TestVoidCommand>>>();
            var behavior = new ExceptionHandlingVoidCommandBehavior<TestVoidCommand>(mockLogger.Object);
            var command = new TestVoidCommand { Input = "test" };
            var expectedException = new InvalidOperationException("Test exception");

            CommandHandlerDelegate next = () => throw expectedException;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await behavior.Handle(command, next, CancellationToken.None));

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
        public async Task Handle_WhenExceptionHandled_ShouldNotRethrow()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingVoidCommandBehavior<TestVoidCommand>>>();
            var mockHandler = new Mock<IExceptionHandler>();

            mockHandler.Setup(h => h.HandleAsync(
                    It.IsAny<Exception>(),
                    It.IsAny<Type>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var behavior = new ExceptionHandlingVoidCommandBehavior<TestVoidCommand>(
                mockLogger.Object,
                exceptionHandler: mockHandler.Object);

            var command = new TestVoidCommand { Input = "test" };
            CommandHandlerDelegate next = () => throw new InvalidOperationException("Test");

            // Act - should not throw
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            mockHandler.Verify(h => h.HandleAsync(
                It.IsAny<Exception>(),
                typeof(TestVoidCommand),
                command,
                CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Handle_WhenCancellationRequested_ShouldRethrowWithoutHandling()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingVoidCommandBehavior<TestVoidCommand>>>();
            var mockHandler = new Mock<IExceptionHandler>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var behavior = new ExceptionHandlingVoidCommandBehavior<TestVoidCommand>(
                mockLogger.Object,
                exceptionHandler: mockHandler.Object);

            var command = new TestVoidCommand { Input = "test" };
            CommandHandlerDelegate next = () => throw new OperationCanceledException(cts.Token);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await behavior.Handle(command, next, cts.Token));

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
            var mockLogger = new Mock<ILogger<ExceptionHandlingVoidCommandBehavior<TestVoidCommand>>>();
            var mockHandler = new Mock<IExceptionHandler>();

            mockHandler.Setup(h => h.HandleAsync(
                    It.IsAny<Exception>(),
                    It.IsAny<Type>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var behavior = new ExceptionHandlingVoidCommandBehavior<TestVoidCommand>(
                mockLogger.Object,
                exceptionHandler: mockHandler.Object);

            var command = new TestVoidCommand { Input = "test" };
            var expectedException = new InvalidOperationException("Test");
            CommandHandlerDelegate next = () => throw expectedException;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await behavior.Handle(command, next, CancellationToken.None));

            Assert.Same(expectedException, exception);
        }
    }
}
