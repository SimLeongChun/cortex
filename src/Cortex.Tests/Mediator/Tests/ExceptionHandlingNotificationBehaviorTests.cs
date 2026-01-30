using Cortex.Mediator.Behaviors;
using Cortex.Mediator.Notifications;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cortex.Tests.Mediator.Tests
{
    public class ExceptionHandlingNotificationBehaviorTests
    {
        public class TestNotification : INotification
        {
            public string Message { get; set; } = string.Empty;
        }

        [Fact]
        public async Task Handle_WhenNoException_ShouldComplete()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingNotificationBehavior<TestNotification>>>();
            var behavior = new ExceptionHandlingNotificationBehavior<TestNotification>(mockLogger.Object);
            var notification = new TestNotification { Message = "test" };
            var executed = false;

            NotificationHandlerDelegate next = () =>
            {
                executed = true;
                return Task.CompletedTask;
            };

            // Act
            await behavior.Handle(notification, next, CancellationToken.None);

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public async Task Handle_WhenExceptionThrownAndNoHandler_ShouldLogAndRethrow()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingNotificationBehavior<TestNotification>>>();
            var behavior = new ExceptionHandlingNotificationBehavior<TestNotification>(mockLogger.Object);
            var notification = new TestNotification { Message = "test" };
            var expectedException = new InvalidOperationException("Test exception");

            NotificationHandlerDelegate next = () => throw expectedException;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await behavior.Handle(notification, next, CancellationToken.None));

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
            var mockLogger = new Mock<ILogger<ExceptionHandlingNotificationBehavior<TestNotification>>>();
            var mockHandler = new Mock<IExceptionHandler>();

            mockHandler.Setup(h => h.HandleAsync(
                    It.IsAny<Exception>(),
                    It.IsAny<Type>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var behavior = new ExceptionHandlingNotificationBehavior<TestNotification>(
                mockLogger.Object,
                exceptionHandler: mockHandler.Object);

            var notification = new TestNotification { Message = "test" };
            NotificationHandlerDelegate next = () => throw new InvalidOperationException("Test");

            // Act - should not throw
            await behavior.Handle(notification, next, CancellationToken.None);

            // Assert
            mockHandler.Verify(h => h.HandleAsync(
                It.IsAny<Exception>(),
                typeof(TestNotification),
                notification,
                CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Handle_WhenSuppressExceptionsEnabled_ShouldNotRethrow()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingNotificationBehavior<TestNotification>>>();
            var behavior = new ExceptionHandlingNotificationBehavior<TestNotification>(
                mockLogger.Object,
                suppressExceptions: true);

            var notification = new TestNotification { Message = "test" };
            NotificationHandlerDelegate next = () => throw new InvalidOperationException("Test");

            // Act - should not throw
            await behavior.Handle(notification, next, CancellationToken.None);

            // Verify warning was logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("suppressed")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_WhenSuppressExceptionsDisabled_ShouldRethrow()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingNotificationBehavior<TestNotification>>>();
            var behavior = new ExceptionHandlingNotificationBehavior<TestNotification>(
                mockLogger.Object,
                suppressExceptions: false);

            var notification = new TestNotification { Message = "test" };
            var expectedException = new InvalidOperationException("Test");
            NotificationHandlerDelegate next = () => throw expectedException;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await behavior.Handle(notification, next, CancellationToken.None));

            Assert.Same(expectedException, exception);
        }

        [Fact]
        public async Task Handle_WhenCancellationRequested_ShouldRethrowWithoutHandling()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingNotificationBehavior<TestNotification>>>();
            var mockHandler = new Mock<IExceptionHandler>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var behavior = new ExceptionHandlingNotificationBehavior<TestNotification>(
                mockLogger.Object,
                exceptionHandler: mockHandler.Object,
                suppressExceptions: true); // Even with suppress enabled, cancellation should propagate

            var notification = new TestNotification { Message = "test" };
            NotificationHandlerDelegate next = () => throw new OperationCanceledException(cts.Token);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await behavior.Handle(notification, next, cts.Token));

            // Verify handler was NOT called for cancellation
            mockHandler.Verify(h => h.HandleAsync(
                It.IsAny<Exception>(),
                It.IsAny<Type>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_WhenHandlerReturnsFalseAndSuppressDisabled_ShouldRethrow()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingNotificationBehavior<TestNotification>>>();
            var mockHandler = new Mock<IExceptionHandler>();

            mockHandler.Setup(h => h.HandleAsync(
                    It.IsAny<Exception>(),
                    It.IsAny<Type>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var behavior = new ExceptionHandlingNotificationBehavior<TestNotification>(
                mockLogger.Object,
                exceptionHandler: mockHandler.Object,
                suppressExceptions: false);

            var notification = new TestNotification { Message = "test" };
            var expectedException = new InvalidOperationException("Test");
            NotificationHandlerDelegate next = () => throw expectedException;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await behavior.Handle(notification, next, CancellationToken.None));

            Assert.Same(expectedException, exception);
        }

        [Fact]
        public async Task Handle_WhenHandlerReturnsFalseAndSuppressEnabled_ShouldNotRethrow()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ExceptionHandlingNotificationBehavior<TestNotification>>>();
            var mockHandler = new Mock<IExceptionHandler>();

            mockHandler.Setup(h => h.HandleAsync(
                    It.IsAny<Exception>(),
                    It.IsAny<Type>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var behavior = new ExceptionHandlingNotificationBehavior<TestNotification>(
                mockLogger.Object,
                exceptionHandler: mockHandler.Object,
                suppressExceptions: true);

            var notification = new TestNotification { Message = "test" };
            NotificationHandlerDelegate next = () => throw new InvalidOperationException("Test");

            // Act - should not throw because suppressExceptions is true
            await behavior.Handle(notification, next, CancellationToken.None);

            // Verify warning was logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("suppressed")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
