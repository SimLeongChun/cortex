using Cortex.Mediator.Commands;
using Cortex.Streams;
using Cortex.Streams.Mediator.Behaviors;
using Moq;

namespace Cortex.Tests.StreamsMediator.Tests
{
    #region Test Types

    public class TestCommand : ICommand<TestCommandResult>
    {
        public string Data { get; set; } = string.Empty;
    }

    public class TestCommandResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    #endregion

    public class StreamEmittingCommandBehaviorTests
    {
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenStreamIsNull()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new StreamEmittingCommandBehavior<TestCommand, TestCommandResult>(null!));
        }

        [Fact]
        public async Task Handle_EmitsAfterExecutionEvent_WhenConfigured()
        {
            // Arrange
            var mockStream = new Mock<IStream<CommandExecutionEvent<TestCommand, TestCommandResult>>>();
            CommandExecutionEvent<TestCommand, TestCommandResult>? capturedEvent = null;

            mockStream
                .Setup(s => s.EmitAsync(It.IsAny<CommandExecutionEvent<TestCommand, TestCommandResult>>(), It.IsAny<CancellationToken>()))
                .Callback<CommandExecutionEvent<TestCommand, TestCommandResult>, CancellationToken>((e, _) => capturedEvent = e)
                .Returns(Task.CompletedTask);

            var behavior = new StreamEmittingCommandBehavior<TestCommand, TestCommandResult>(
                mockStream.Object,
                emitBeforeExecution: false,
                emitAfterExecution: true);

            var command = new TestCommand { Data = "test" };
            var expectedResult = new TestCommandResult { Success = true, Message = "OK" };
            CommandHandlerDelegate<TestCommandResult> next = () => Task.FromResult(expectedResult);

            // Act
            var result = await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, result);
            Assert.NotNull(capturedEvent);
            Assert.Equal(CommandExecutionEventType.Succeeded, capturedEvent.EventType);
            Assert.Equal(command, capturedEvent.Command);
            Assert.Equal(expectedResult, capturedEvent.Result);
            Assert.NotNull(capturedEvent.Duration);
            Assert.Null(capturedEvent.Exception);
        }

        [Fact]
        public async Task Handle_EmitsBeforeExecutionEvent_WhenConfigured()
        {
            // Arrange
            var mockStream = new Mock<IStream<CommandExecutionEvent<TestCommand, TestCommandResult>>>();
            var capturedEvents = new List<CommandExecutionEvent<TestCommand, TestCommandResult>>();

            mockStream
                .Setup(s => s.EmitAsync(It.IsAny<CommandExecutionEvent<TestCommand, TestCommandResult>>(), It.IsAny<CancellationToken>()))
                .Callback<CommandExecutionEvent<TestCommand, TestCommandResult>, CancellationToken>((e, _) => capturedEvents.Add(e))
                .Returns(Task.CompletedTask);

            var behavior = new StreamEmittingCommandBehavior<TestCommand, TestCommandResult>(
                mockStream.Object,
                emitBeforeExecution: true,
                emitAfterExecution: true);

            var command = new TestCommand { Data = "test" };
            CommandHandlerDelegate<TestCommandResult> next = () => Task.FromResult(new TestCommandResult { Success = true });

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.Equal(2, capturedEvents.Count);
            Assert.Equal(CommandExecutionEventType.BeforeExecution, capturedEvents[0].EventType);
            Assert.Equal(CommandExecutionEventType.Succeeded, capturedEvents[1].EventType);
        }

        [Fact]
        public async Task Handle_EmitsFailedEvent_WhenCommandThrows()
        {
            // Arrange
            var mockStream = new Mock<IStream<CommandExecutionEvent<TestCommand, TestCommandResult>>>();
            CommandExecutionEvent<TestCommand, TestCommandResult>? capturedEvent = null;

            mockStream
                .Setup(s => s.EmitAsync(It.IsAny<CommandExecutionEvent<TestCommand, TestCommandResult>>(), It.IsAny<CancellationToken>()))
                .Callback<CommandExecutionEvent<TestCommand, TestCommandResult>, CancellationToken>((e, _) => capturedEvent = e)
                .Returns(Task.CompletedTask);

            var behavior = new StreamEmittingCommandBehavior<TestCommand, TestCommandResult>(
                mockStream.Object,
                emitBeforeExecution: false,
                emitAfterExecution: true);

            var command = new TestCommand { Data = "test" };
            var expectedException = new InvalidOperationException("Command failed");
            CommandHandlerDelegate<TestCommandResult> next = () => throw expectedException;

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                behavior.Handle(command, next, CancellationToken.None));

            Assert.NotNull(capturedEvent);
            Assert.Equal(CommandExecutionEventType.Failed, capturedEvent.EventType);
            Assert.NotNull(capturedEvent.Exception);
            Assert.IsType<InvalidOperationException>(capturedEvent.Exception);
        }

        [Fact]
        public async Task Handle_DoesNotEmit_WhenBothFlagsAreFalse()
        {
            // Arrange
            var mockStream = new Mock<IStream<CommandExecutionEvent<TestCommand, TestCommandResult>>>();

            var behavior = new StreamEmittingCommandBehavior<TestCommand, TestCommandResult>(
                mockStream.Object,
                emitBeforeExecution: false,
                emitAfterExecution: false);

            var command = new TestCommand { Data = "test" };
            CommandHandlerDelegate<TestCommandResult> next = () => Task.FromResult(new TestCommandResult { Success = true });

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            mockStream.Verify(s => s.EmitAsync(
                It.IsAny<CommandExecutionEvent<TestCommand, TestCommandResult>>(), 
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_IncludesDuration_InAfterEvent()
        {
            // Arrange
            var mockStream = new Mock<IStream<CommandExecutionEvent<TestCommand, TestCommandResult>>>();
            CommandExecutionEvent<TestCommand, TestCommandResult>? capturedEvent = null;

            mockStream
                .Setup(s => s.EmitAsync(It.IsAny<CommandExecutionEvent<TestCommand, TestCommandResult>>(), It.IsAny<CancellationToken>()))
                .Callback<CommandExecutionEvent<TestCommand, TestCommandResult>, CancellationToken>((e, _) => capturedEvent = e)
                .Returns(Task.CompletedTask);

            var behavior = new StreamEmittingCommandBehavior<TestCommand, TestCommandResult>(
                mockStream.Object,
                emitBeforeExecution: false,
                emitAfterExecution: true);

            var command = new TestCommand { Data = "test" };
            CommandHandlerDelegate<TestCommandResult> next = async () =>
            {
                await Task.Delay(50); // Add some delay
                return new TestCommandResult { Success = true };
            };

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedEvent);
            Assert.NotNull(capturedEvent.Duration);
            Assert.True(capturedEvent.Duration.Value.TotalMilliseconds >= 40); // Allow some tolerance
        }

        [Fact]
        public async Task Handle_PropagatesResultCorrectly()
        {
            // Arrange
            var mockStream = new Mock<IStream<CommandExecutionEvent<TestCommand, TestCommandResult>>>();
            mockStream
                .Setup(s => s.EmitAsync(It.IsAny<CommandExecutionEvent<TestCommand, TestCommandResult>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var behavior = new StreamEmittingCommandBehavior<TestCommand, TestCommandResult>(
                mockStream.Object,
                emitBeforeExecution: false,
                emitAfterExecution: true);

            var command = new TestCommand { Data = "test" };
            var expectedResult = new TestCommandResult { Success = true, Message = "Expected result" };
            CommandHandlerDelegate<TestCommandResult> next = () => Task.FromResult(expectedResult);

            // Act
            var result = await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.Same(expectedResult, result);
        }
    }
}
