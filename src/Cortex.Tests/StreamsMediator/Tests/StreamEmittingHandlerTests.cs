using Cortex.Mediator.Notifications;
using Cortex.Streams;
using Cortex.Streams.Mediator.Handlers;
using Moq;

namespace Cortex.Tests.StreamsMediator.Tests
{
    #region Test Types

    public class OrderCreatedNotification : INotification
    {
        public string OrderId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class OrderStreamData
    {
        public string OrderId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    #endregion

    public class StreamEmittingNotificationHandlerTests
    {
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenStreamIsNull()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new StreamEmittingNotificationHandler<OrderCreatedNotification>(null!));
        }

        [Fact]
        public async Task Handle_EmitsNotificationToStream()
        {
            // Arrange
            var mockStream = new Mock<IStream<OrderCreatedNotification, OrderCreatedNotification>>();
            OrderCreatedNotification? capturedNotification = null;

            mockStream
                .Setup(s => s.EmitAsync(It.IsAny<OrderCreatedNotification>(), It.IsAny<CancellationToken>()))
                .Callback<OrderCreatedNotification, CancellationToken>((n, _) => capturedNotification = n)
                .Returns(Task.CompletedTask);

            var handler = new StreamEmittingNotificationHandler<OrderCreatedNotification>(mockStream.Object);
            var notification = new OrderCreatedNotification 
            { 
                OrderId = "ORD-001", 
                CreatedAt = DateTime.UtcNow 
            };

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedNotification);
            Assert.Equal("ORD-001", capturedNotification.OrderId);
            mockStream.Verify(s => s.EmitAsync(notification, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_InvokesErrorHandler_WhenStreamEmitFails()
        {
            // Arrange
            var mockStream = new Mock<IStream<OrderCreatedNotification, OrderCreatedNotification>>();
            OrderCreatedNotification? capturedNotification = null;
            Exception? capturedException = null;

            mockStream
                .Setup(s => s.EmitAsync(It.IsAny<OrderCreatedNotification>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Stream error"));

            var handler = new StreamEmittingNotificationHandler<OrderCreatedNotification>(
                mockStream.Object,
                errorHandler: (n, ex) =>
                {
                    capturedNotification = n;
                    capturedException = ex;
                });

            var notification = new OrderCreatedNotification { OrderId = "ORD-002" };

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedNotification);
            Assert.Equal("ORD-002", capturedNotification.OrderId);
            Assert.NotNull(capturedException);
            Assert.IsType<InvalidOperationException>(capturedException);
        }

        [Fact]
        public async Task Handle_ThrowsException_WhenNoErrorHandlerAndStreamFails()
        {
            // Arrange
            var mockStream = new Mock<IStream<OrderCreatedNotification, OrderCreatedNotification>>();

            mockStream
                .Setup(s => s.EmitAsync(It.IsAny<OrderCreatedNotification>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Stream error"));

            var handler = new StreamEmittingNotificationHandler<OrderCreatedNotification>(mockStream.Object);
            var notification = new OrderCreatedNotification { OrderId = "ORD-003" };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(notification, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PassesCancellationToken_ToStream()
        {
            // Arrange
            var mockStream = new Mock<IStream<OrderCreatedNotification, OrderCreatedNotification>>();
            CancellationToken capturedToken = default;

            mockStream
                .Setup(s => s.EmitAsync(It.IsAny<OrderCreatedNotification>(), It.IsAny<CancellationToken>()))
                .Callback<OrderCreatedNotification, CancellationToken>((_, ct) => capturedToken = ct)
                .Returns(Task.CompletedTask);

            var handler = new StreamEmittingNotificationHandler<OrderCreatedNotification>(mockStream.Object);
            using var cts = new CancellationTokenSource();
            var notification = new OrderCreatedNotification { OrderId = "ORD-004" };

            // Act
            await handler.Handle(notification, cts.Token);

            // Assert
            Assert.Equal(cts.Token, capturedToken);
        }
    }

    public class TransformingStreamNotificationHandlerTests
    {
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenStreamIsNull()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TransformingStreamNotificationHandler<OrderCreatedNotification, OrderStreamData>(
                    null!,
                    n => new OrderStreamData()));
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenTransformerIsNull()
        {
            // Arrange
            var mockStream = new Mock<IStream<OrderStreamData, OrderStreamData>>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TransformingStreamNotificationHandler<OrderCreatedNotification, OrderStreamData>(
                    mockStream.Object,
                    null!));
        }

        [Fact]
        public async Task Handle_TransformsAndEmitsNotificationToStream()
        {
            // Arrange
            var mockStream = new Mock<IStream<OrderStreamData, OrderStreamData>>();
            OrderStreamData? capturedData = null;

            mockStream
                .Setup(s => s.EmitAsync(It.IsAny<OrderStreamData>(), It.IsAny<CancellationToken>()))
                .Callback<OrderStreamData, CancellationToken>((d, _) => capturedData = d)
                .Returns(Task.CompletedTask);

            var handler = new TransformingStreamNotificationHandler<OrderCreatedNotification, OrderStreamData>(
                mockStream.Object,
                notification => new OrderStreamData 
                { 
                    OrderId = notification.OrderId, 
                    Status = "Created" 
                });

            var notification = new OrderCreatedNotification 
            { 
                OrderId = "ORD-001", 
                CreatedAt = DateTime.UtcNow 
            };

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedData);
            Assert.Equal("ORD-001", capturedData.OrderId);
            Assert.Equal("Created", capturedData.Status);
        }

        [Fact]
        public async Task Handle_InvokesErrorHandler_WhenTransformFails()
        {
            // Arrange
            var mockStream = new Mock<IStream<OrderStreamData, OrderStreamData>>();
            Exception? capturedException = null;

            var handler = new TransformingStreamNotificationHandler<OrderCreatedNotification, OrderStreamData>(
                mockStream.Object,
                notification => throw new InvalidOperationException("Transform failed"),
                errorHandler: (_, ex) => capturedException = ex);

            var notification = new OrderCreatedNotification { OrderId = "ORD-002" };

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedException);
            Assert.IsType<InvalidOperationException>(capturedException);
            mockStream.Verify(s => s.EmitAsync(It.IsAny<OrderStreamData>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_InvokesErrorHandler_WhenStreamEmitFails()
        {
            // Arrange
            var mockStream = new Mock<IStream<OrderStreamData, OrderStreamData>>();
            Exception? capturedException = null;

            mockStream
                .Setup(s => s.EmitAsync(It.IsAny<OrderStreamData>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Stream error"));

            var handler = new TransformingStreamNotificationHandler<OrderCreatedNotification, OrderStreamData>(
                mockStream.Object,
                notification => new OrderStreamData { OrderId = notification.OrderId },
                errorHandler: (_, ex) => capturedException = ex);

            var notification = new OrderCreatedNotification { OrderId = "ORD-003" };

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedException);
            Assert.IsType<InvalidOperationException>(capturedException);
        }
    }
}
