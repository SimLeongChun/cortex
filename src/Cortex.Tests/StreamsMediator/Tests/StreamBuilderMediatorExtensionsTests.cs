using Cortex.Mediator;
using Cortex.Mediator.Commands;
using Cortex.Mediator.Notifications;
using Cortex.Streams;
using Cortex.Streams.Abstractions;
using Cortex.Streams.Mediator.Extensions;
using Moq;

namespace Cortex.Tests.StreamsMediator.Tests
{
    #region Test Types

    public class StreamExtensionTestCommand : ICommand<string>
    {
        public string Input { get; set; } = string.Empty;
    }

    public class StreamExtensionVoidCommand : ICommand
    {
        public string Data { get; set; } = string.Empty;
    }

    public class StreamExtensionNotification : INotification
    {
        public string Message { get; set; } = string.Empty;
    }

    #endregion

    public class StreamBuilderMediatorExtensionsTests
    {
        [Fact]
        public void SinkToCommand_CreatesSinkWithCorrectBehavior()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var capturedCommands = new List<StreamExtensionTestCommand>();

            mockMediator
                .Setup(m => m.SendCommandAsync<StreamExtensionTestCommand, string>(
                    It.IsAny<StreamExtensionTestCommand>(),
                    It.IsAny<CancellationToken>()))
                .Callback<StreamExtensionTestCommand, CancellationToken>((cmd, _) => capturedCommands.Add(cmd))
                .ReturnsAsync("result");

            // Build a stream using the extension method
            var stream = StreamBuilder<string, string>
                .CreateNewStream("TestStream")
                .Stream()
                .SinkToCommand<string, string, StreamExtensionTestCommand, string>(
                    mockMediator.Object,
                    input => new StreamExtensionTestCommand { Input = input },
                    resultHandler: (input, result) => { })
                .Build();

            // Act
            stream.Start();
            stream.Emit("test-input-1");
            stream.Emit("test-input-2");
            stream.Stop();

            // Assert
            Assert.Equal(2, capturedCommands.Count);
            Assert.Equal("test-input-1", capturedCommands[0].Input);
            Assert.Equal("test-input-2", capturedCommands[1].Input);
        }

        [Fact]
        public void SinkToVoidCommand_CreatesSinkWithCorrectBehavior()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var capturedCommands = new List<StreamExtensionVoidCommand>();

            mockMediator
                .Setup(m => m.SendCommandAsync<StreamExtensionVoidCommand>(
                    It.IsAny<StreamExtensionVoidCommand>(),
                    It.IsAny<CancellationToken>()))
                .Callback<StreamExtensionVoidCommand, CancellationToken>((cmd, _) => capturedCommands.Add(cmd))
                .Returns(Task.CompletedTask);

            // Build a stream using the extension method
            var stream = StreamBuilder<string, string>
                .CreateNewStream("TestStream")
                .Stream()
                .SinkToVoidCommand<string, string, StreamExtensionVoidCommand>(
                    mockMediator.Object,
                    input => new StreamExtensionVoidCommand { Data = input })
                .Build();

            // Act
            stream.Start();
            stream.Emit("data-1");
            stream.Emit("data-2");
            stream.Stop();

            // Assert
            Assert.Equal(2, capturedCommands.Count);
            Assert.Equal("data-1", capturedCommands[0].Data);
            Assert.Equal("data-2", capturedCommands[1].Data);
        }

        [Fact]
        public void SinkToNotification_CreatesSinkWithCorrectBehavior()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var capturedNotifications = new List<StreamExtensionNotification>();

            mockMediator
                .Setup(m => m.PublishAsync(
                    It.IsAny<StreamExtensionNotification>(),
                    It.IsAny<CancellationToken>()))
                .Callback<StreamExtensionNotification, CancellationToken>((n, _) => capturedNotifications.Add(n))
                .Returns(Task.CompletedTask);

            // Build a stream using the extension method
            var stream = StreamBuilder<string, string>
                .CreateNewStream("TestStream")
                .Stream()
                .SinkToNotification<string, string, StreamExtensionNotification>(
                    mockMediator.Object,
                    input => new StreamExtensionNotification { Message = input })
                .Build();

            // Act
            stream.Start();
            stream.Emit("message-1");
            stream.Emit("message-2");
            stream.Stop();

            // Assert
            Assert.Equal(2, capturedNotifications.Count);
            Assert.Equal("message-1", capturedNotifications[0].Message);
            Assert.Equal("message-2", capturedNotifications[1].Message);
        }

        [Fact]
        public void PublishNotification_WorksWithNotificationTypeDirectly()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var capturedNotifications = new List<StreamExtensionNotification>();

            mockMediator
                .Setup(m => m.PublishAsync(
                    It.IsAny<StreamExtensionNotification>(),
                    It.IsAny<CancellationToken>()))
                .Callback<StreamExtensionNotification, CancellationToken>((n, _) => capturedNotifications.Add(n))
                .Returns(Task.CompletedTask);

            // Build a stream using the extension method - starts with notification type
            var stream = StreamBuilder<StreamExtensionNotification, StreamExtensionNotification>
                .CreateNewStream("NotificationStream")
                .Stream()
                .PublishNotification(mockMediator.Object)
                .Build();

            // Act
            stream.Start();
            stream.Emit(new StreamExtensionNotification { Message = "direct-1" });
            stream.Emit(new StreamExtensionNotification { Message = "direct-2" });
            stream.Stop();

            // Assert
            Assert.Equal(2, capturedNotifications.Count);
            Assert.Equal("direct-1", capturedNotifications[0].Message);
            Assert.Equal("direct-2", capturedNotifications[1].Message);
        }

        [Fact]
        public void SinkToCommand_InvokesResultHandler()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var capturedResults = new List<(string input, string result)>();

            mockMediator
                .Setup(m => m.SendCommandAsync<StreamExtensionTestCommand, string>(
                    It.IsAny<StreamExtensionTestCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((StreamExtensionTestCommand cmd, CancellationToken _) => $"processed-{cmd.Input}");

            // Build a stream using the extension method
            var stream = StreamBuilder<string, string>
                .CreateNewStream("TestStream")
                .Stream()
                .SinkToCommand<string, string, StreamExtensionTestCommand, string>(
                    mockMediator.Object,
                    input => new StreamExtensionTestCommand { Input = input },
                    resultHandler: (string input, string result) => capturedResults.Add((input, result)))
                .Build();

            // Act
            stream.Start();
            stream.Emit("input-1");
            stream.Stop();

            // Assert
            Assert.Single(capturedResults);
            Assert.Equal("input-1", capturedResults[0].input);
            Assert.Equal("processed-input-1", capturedResults[0].result);
        }

        [Fact]
        public void SinkToCommand_InvokesErrorHandler_OnException()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var capturedErrors = new List<(string input, Exception ex)>();

            mockMediator
                .Setup(m => m.SendCommandAsync<StreamExtensionTestCommand, string>(
                    It.IsAny<StreamExtensionTestCommand>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test error"));

            // Build a stream using the extension method
            var stream = StreamBuilder<string, string>
                .CreateNewStream("TestStream")
                .Stream()
                .SinkToCommand<string, string, StreamExtensionTestCommand, string>(
                    mockMediator.Object,
                    input => new StreamExtensionTestCommand { Input = input },
                    errorHandler: (string input, Exception ex) => capturedErrors.Add((input, ex)))
                .Build();

            // Act
            stream.Start();
            stream.Emit("error-input");
            stream.Stop();

            // Assert
            Assert.Single(capturedErrors);
            Assert.Equal("error-input", capturedErrors[0].input);
            Assert.IsType<InvalidOperationException>(capturedErrors[0].ex);
        }

        [Fact]
        public void SinkToNotification_InvokesCompletionHandler()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var capturedCompletions = new List<string>();



            mockMediator
                .Setup(m => m.PublishAsync(
                    It.IsAny<StreamExtensionNotification>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Build a stream using the extension method
            var stream = StreamBuilder<string, string>
                .CreateNewStream("TestStream")
                .Stream()
                .SinkToNotification<string, string, StreamExtensionNotification>(
                    mockMediator.Object,
                    input => new StreamExtensionNotification { Message = input },
                    completionHandler: input => capturedCompletions.Add(input))
                .Build();

            // Act
            stream.Start();
            stream.Emit("completed-1");
            stream.Emit("completed-2");
            stream.Stop();

            // Assert
            Assert.Equal(2, capturedCompletions.Count);
            Assert.Contains("completed-1", capturedCompletions);
            Assert.Contains("completed-2", capturedCompletions);
        }
    }
}
