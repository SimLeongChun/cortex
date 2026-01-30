using Cortex.Mediator;
using Cortex.Mediator.Notifications;
using Cortex.Streams.Mediator.Operators;
using Moq;

namespace Cortex.Tests.StreamsMediator.Tests
{
    #region Test Notifications

    public class OrderProcessedNotification : INotification
    {
        public string OrderId { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
    }

    public class TestNotification : INotification
    {
        public string Message { get; set; } = string.Empty;
    }

    #endregion

    public class MediatorNotificationSinkOperatorTests
    {
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenMediatorIsNull()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorNotificationSinkOperator<string, OrderProcessedNotification>(
                    null!,
                    _ => new OrderProcessedNotification()));
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenNotificationFactoryIsNull()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorNotificationSinkOperator<string, OrderProcessedNotification>(
                    mockMediator.Object,
                    null!));
        }

        [Fact]
        public void Process_PublishesNotificationThroughMediator()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            
            mockMediator
                .Setup(m => m.PublishAsync(
                    It.IsAny<OrderProcessedNotification>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sinkOperator = new MediatorNotificationSinkOperator<string, OrderProcessedNotification>(
                mockMediator.Object,
                input => new OrderProcessedNotification 
                { 
                    OrderId = input, 
                    ProcessedAt = DateTime.UtcNow 
                });

            // Act
            sinkOperator.Process("ORDER-001");

            // Assert
            mockMediator.Verify(m => m.PublishAsync(
                It.Is<OrderProcessedNotification>(n => n.OrderId == "ORDER-001"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Process_InvokesCompletionHandler_WhenProvided()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            string? capturedInput = null;

            mockMediator
                .Setup(m => m.PublishAsync(
                    It.IsAny<OrderProcessedNotification>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sinkOperator = new MediatorNotificationSinkOperator<string, OrderProcessedNotification>(
                mockMediator.Object,
                input => new OrderProcessedNotification { OrderId = input },
                completionHandler: input => capturedInput = input);

            // Act
            sinkOperator.Process("ORDER-002");

            // Assert
            Assert.Equal("ORDER-002", capturedInput);
        }

        [Fact]
        public void Process_ThrowsException_WhenErrorOccurs()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();

            mockMediator
                .Setup(m => m.PublishAsync(
                    It.IsAny<OrderProcessedNotification>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Publish failed"));

            var sinkOperator = new MediatorNotificationSinkOperator<string, OrderProcessedNotification>(
                mockMediator.Object,
                input => new OrderProcessedNotification { OrderId = input });

            // Act & Assert - Exception should propagate (stream-level error handling handles it)
            Assert.Throws<InvalidOperationException>(() => sinkOperator.Process("ORDER-003"));
        }

        [Fact]
        public void Process_ThrowsException_WhenMediatorFails()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();

            mockMediator
                .Setup(m => m.PublishAsync(
                    It.IsAny<OrderProcessedNotification>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Publish failed"));

            var sinkOperator = new MediatorNotificationSinkOperator<string, OrderProcessedNotification>(
                mockMediator.Object,
                input => new OrderProcessedNotification { OrderId = input });

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => sinkOperator.Process("ORDER-004"));
        }
    }

    public class MediatorDirectNotificationSinkOperatorTests
    {
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenMediatorIsNull()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorDirectNotificationSinkOperator<TestNotification>(null!));
        }

        [Fact]
        public void Process_PublishesNotificationDirectly()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            
            mockMediator
                .Setup(m => m.PublishAsync(
                    It.IsAny<TestNotification>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sinkOperator = new MediatorDirectNotificationSinkOperator<TestNotification>(mockMediator.Object);
            var notification = new TestNotification { Message = "Direct publish" };

            // Act
            sinkOperator.Process(notification);

            // Assert
            mockMediator.Verify(m => m.PublishAsync(
                It.Is<TestNotification>(n => n.Message == "Direct publish"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Process_InvokesCompletionHandler_WhenProvided()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            TestNotification? capturedNotification = null;

            mockMediator
                .Setup(m => m.PublishAsync(
                    It.IsAny<TestNotification>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sinkOperator = new MediatorDirectNotificationSinkOperator<TestNotification>(
                mockMediator.Object,
                completionHandler: n => capturedNotification = n);

            var notification = new TestNotification { Message = "Test" };


            // Act
            sinkOperator.Process(notification);

            // Assert
            Assert.NotNull(capturedNotification);
            Assert.Equal("Test", capturedNotification.Message);
        }

        [Fact]
        public void Process_ThrowsException_WhenErrorOccurs()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();

            mockMediator
                .Setup(m => m.PublishAsync(
                    It.IsAny<TestNotification>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Publish failed"));

            var sinkOperator = new MediatorDirectNotificationSinkOperator<TestNotification>(
                mockMediator.Object);

            // Act & Assert - Exception should propagate (stream-level error handling handles it)
            Assert.Throws<InvalidOperationException>(() => sinkOperator.Process(new TestNotification { Message = "Error" }));
        }
    }
}
