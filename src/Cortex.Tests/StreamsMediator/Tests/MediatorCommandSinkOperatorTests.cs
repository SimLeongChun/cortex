using Cortex.Mediator;
using Cortex.Mediator.Commands;
using Cortex.Mediator.Notifications;
using Cortex.Streams.Mediator.Operators;
using Moq;

namespace Cortex.Tests.StreamsMediator.Tests
{
    #region Test Commands

    public class ProcessOrderCommand : ICommand<OrderResult>
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class OrderResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SaveDataCommand : ICommand
    {
        public string Data { get; set; } = string.Empty;
    }

    #endregion

    public class MediatorCommandSinkOperatorTests
    {
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenMediatorIsNull()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorCommandSinkOperator<string, ProcessOrderCommand, OrderResult>(
                    null!,
                    _ => new ProcessOrderCommand()));
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenCommandFactoryIsNull()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorCommandSinkOperator<string, ProcessOrderCommand, OrderResult>(
                    mockMediator.Object,
                    null!));
        }

        [Fact]
        public void Process_SendsCommandThroughMediator()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var expectedResult = new OrderResult { Success = true, Message = "OK" };
            
            mockMediator
                .Setup(m => m.SendCommandAsync<ProcessOrderCommand, OrderResult>(
                    It.IsAny<ProcessOrderCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var sinkOperator = new MediatorCommandSinkOperator<string, ProcessOrderCommand, OrderResult>(
                mockMediator.Object,
                input => new ProcessOrderCommand { OrderId = input, Amount = 100m });

            // Act
            sinkOperator.Process("ORDER-001");

            // Assert
            mockMediator.Verify(m => m.SendCommandAsync<ProcessOrderCommand, OrderResult>(
                It.Is<ProcessOrderCommand>(c => c.OrderId == "ORDER-001" && c.Amount == 100m),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Process_InvokesResultHandler_WhenProvided()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var expectedResult = new OrderResult { Success = true, Message = "Processed" };
            OrderResult? capturedResult = null;
            string? capturedInput = null;

            mockMediator
                .Setup(m => m.SendCommandAsync<ProcessOrderCommand, OrderResult>(
                    It.IsAny<ProcessOrderCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var sinkOperator = new MediatorCommandSinkOperator<string, ProcessOrderCommand, OrderResult>(
                mockMediator.Object,
                input => new ProcessOrderCommand { OrderId = input },
                resultHandler: (input, result) =>
                {
                    capturedInput = input;
                    capturedResult = result;
                });

            // Act
            sinkOperator.Process("ORDER-002");

            // Assert
            Assert.Equal("ORDER-002", capturedInput);
            Assert.NotNull(capturedResult);
            Assert.True(capturedResult.Success);
            Assert.Equal("Processed", capturedResult.Message);
        }

        [Fact]
        public void Process_InvokesErrorHandler_WhenExceptionOccurs()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var expectedException = new InvalidOperationException("Test error");
            Exception? capturedException = null;
            string? capturedInput = null;

            mockMediator
                .Setup(m => m.SendCommandAsync<ProcessOrderCommand, OrderResult>(
                    It.IsAny<ProcessOrderCommand>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            var sinkOperator = new MediatorCommandSinkOperator<string, ProcessOrderCommand, OrderResult>(
                mockMediator.Object,
                input => new ProcessOrderCommand { OrderId = input },
                errorHandler: (input, ex) =>
                {
                    capturedInput = input;
                    capturedException = ex;
                });

            // Act
            sinkOperator.Process("ORDER-003");

            // Assert
            Assert.Equal("ORDER-003", capturedInput);
            Assert.NotNull(capturedException);
            Assert.IsType<InvalidOperationException>(capturedException);
        }

        [Fact]
        public void Process_ThrowsException_WhenNoErrorHandlerProvided()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            
            mockMediator
                .Setup(m => m.SendCommandAsync<ProcessOrderCommand, OrderResult>(
                    It.IsAny<ProcessOrderCommand>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test error"));

            var sinkOperator = new MediatorCommandSinkOperator<string, ProcessOrderCommand, OrderResult>(
                mockMediator.Object,
                input => new ProcessOrderCommand { OrderId = input });

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => sinkOperator.Process("ORDER-004"));
        }

        [Fact]
        public void Start_DoesNotThrow()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var sinkOperator = new MediatorCommandSinkOperator<string, ProcessOrderCommand, OrderResult>(
                mockMediator.Object,
                input => new ProcessOrderCommand { OrderId = input });

            // Act & Assert
            var exception = Record.Exception(() => sinkOperator.Start());
            Assert.Null(exception);
        }

        [Fact]
        public void Stop_DoesNotThrow()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var sinkOperator = new MediatorCommandSinkOperator<string, ProcessOrderCommand, OrderResult>(
                mockMediator.Object,
                input => new ProcessOrderCommand { OrderId = input });

            // Act & Assert
            var exception = Record.Exception(() => sinkOperator.Stop());
            Assert.Null(exception);
        }
    }

    public class MediatorVoidCommandSinkOperatorTests
    {
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenMediatorIsNull()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorVoidCommandSinkOperator<string, SaveDataCommand>(
                    null!,
                    _ => new SaveDataCommand()));
        }

        [Fact]
        public void Process_SendsVoidCommandThroughMediator()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            
            mockMediator
                .Setup(m => m.SendCommandAsync<SaveDataCommand>(
                    It.IsAny<SaveDataCommand>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sinkOperator = new MediatorVoidCommandSinkOperator<string, SaveDataCommand>(
                mockMediator.Object,
                input => new SaveDataCommand { Data = input });

            // Act
            sinkOperator.Process("test-data");

            // Assert
            mockMediator.Verify(m => m.SendCommandAsync<SaveDataCommand>(
                It.Is<SaveDataCommand>(c => c.Data == "test-data"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void Process_InvokesCompletionHandler_WhenProvided()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            string? capturedInput = null;

            mockMediator
                .Setup(m => m.SendCommandAsync<SaveDataCommand>(
                    It.IsAny<SaveDataCommand>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var sinkOperator = new MediatorVoidCommandSinkOperator<string, SaveDataCommand>(
                mockMediator.Object,
                input => new SaveDataCommand { Data = input },
                completionHandler: input => capturedInput = input);

            // Act
            sinkOperator.Process("completed-data");

            // Assert
            Assert.Equal("completed-data", capturedInput);
        }

        [Fact]
        public void Process_InvokesErrorHandler_WhenExceptionOccurs()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            Exception? capturedException = null;

            mockMediator
                .Setup(m => m.SendCommandAsync<SaveDataCommand>(
                    It.IsAny<SaveDataCommand>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Save failed"));

            var sinkOperator = new MediatorVoidCommandSinkOperator<string, SaveDataCommand>(
                mockMediator.Object,
                input => new SaveDataCommand { Data = input },
                errorHandler: (_, ex) => capturedException = ex);

            // Act
            sinkOperator.Process("error-data");

            // Assert
            Assert.NotNull(capturedException);
            Assert.IsType<InvalidOperationException>(capturedException);
        }
    }
}
