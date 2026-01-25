using Cortex.Mediator.Commands;
using Cortex.Mediator.Processors;
using Moq;

namespace Cortex.Tests.Mediator.Tests
{
    public class RequestPostProcessorBehaviorTests
    {
        #region Test Commands

        public class TestCommand : ICommand<string>
        {
            public string Input { get; set; } = string.Empty;
        }

        public class TestVoidCommand : ICommand
        {
            public string Input { get; set; } = string.Empty;
        }

        #endregion

        [Fact]
        public async Task Handle_ShouldExecuteAllPostProcessorsAfterHandler()
        {
            // Arrange
            var executionOrder = new List<string>();

            var postProcessor1 = new Mock<IRequestPostProcessor<TestCommand, string>>();
            postProcessor1.Setup(p => p.ProcessAsync(It.IsAny<TestCommand>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => executionOrder.Add("PostProcessor1"))
                .Returns(Task.CompletedTask);

            var postProcessor2 = new Mock<IRequestPostProcessor<TestCommand, string>>();
            postProcessor2.Setup(p => p.ProcessAsync(It.IsAny<TestCommand>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => executionOrder.Add("PostProcessor2"))
                .Returns(Task.CompletedTask);

            var behavior = new RequestPostProcessorBehavior<TestCommand, string>(
                new[] { postProcessor1.Object, postProcessor2.Object });

            var command = new TestCommand { Input = "test" };
            CommandHandlerDelegate<string> next = () =>
            {
                executionOrder.Add("Handler");
                return Task.FromResult("result");
            };

            // Act
            var result = await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.Equal("result", result);
            Assert.Equal(3, executionOrder.Count);
            Assert.Equal("Handler", executionOrder[0]);
            Assert.Equal("PostProcessor1", executionOrder[1]);
            Assert.Equal("PostProcessor2", executionOrder[2]);
        }

        [Fact]
        public async Task Handle_WithNoPostProcessors_ShouldReturnHandlerResult()
        {
            // Arrange
            var behavior = new RequestPostProcessorBehavior<TestCommand, string>(
                Enumerable.Empty<IRequestPostProcessor<TestCommand, string>>());

            var command = new TestCommand { Input = "test" };
            CommandHandlerDelegate<string> next = () => Task.FromResult("result");

            // Act
            var result = await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.Equal("result", result);
        }

        [Fact]
        public async Task Handle_VoidCommand_ShouldExecutePostProcessors()
        {
            // Arrange
            var executionOrder = new List<string>();

            var postProcessor = new Mock<IRequestPostProcessor<TestVoidCommand>>();
            postProcessor.Setup(p => p.ProcessAsync(It.IsAny<TestVoidCommand>(), It.IsAny<CancellationToken>()))
                .Callback(() => executionOrder.Add("PostProcessor"))
                .Returns(Task.CompletedTask);

            var behavior = new RequestPostProcessorBehavior<TestVoidCommand>(
                new[] { postProcessor.Object });

            var command = new TestVoidCommand { Input = "test" };
            CommandHandlerDelegate next = () =>
            {
                executionOrder.Add("Handler");
                return Task.CompletedTask;
            };

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.Equal(2, executionOrder.Count);
            Assert.Equal("Handler", executionOrder[0]);
            Assert.Equal("PostProcessor", executionOrder[1]);
        }

        [Fact]
        public async Task Handle_ShouldPassCommandAndResponseToPostProcessors()
        {
            // Arrange
            TestCommand capturedCommand = null!;
            string capturedResponse = null!;

            var postProcessor = new Mock<IRequestPostProcessor<TestCommand, string>>();
            postProcessor.Setup(p => p.ProcessAsync(It.IsAny<TestCommand>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<TestCommand, string, CancellationToken>((cmd, resp, ct) =>
                {
                    capturedCommand = cmd;
                    capturedResponse = resp;
                })
                .Returns(Task.CompletedTask);

            var behavior = new RequestPostProcessorBehavior<TestCommand, string>(
                new[] { postProcessor.Object });

            var command = new TestCommand { Input = "test-input" };
            CommandHandlerDelegate<string> next = () => Task.FromResult("test-response");

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal("test-input", capturedCommand.Input);
            Assert.Equal("test-response", capturedResponse);
        }

        [Fact]
        public async Task Handle_ShouldPassCancellationToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            CancellationToken capturedToken = default;

            var postProcessor = new Mock<IRequestPostProcessor<TestCommand, string>>();
            postProcessor.Setup(p => p.ProcessAsync(It.IsAny<TestCommand>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<TestCommand, string, CancellationToken>((cmd, resp, ct) => capturedToken = ct)
                .Returns(Task.CompletedTask);

            var behavior = new RequestPostProcessorBehavior<TestCommand, string>(
                new[] { postProcessor.Object });

            var command = new TestCommand { Input = "test" };
            CommandHandlerDelegate<string> next = () => Task.FromResult("result");

            // Act
            await behavior.Handle(command, next, cts.Token);

            // Assert
            Assert.Equal(cts.Token, capturedToken);
        }

        [Fact]
        public async Task Handle_WhenPostProcessorThrows_ShouldPropagateException()
        {
            // Arrange
            var postProcessor = new Mock<IRequestPostProcessor<TestCommand, string>>();
            postProcessor.Setup(p => p.ProcessAsync(It.IsAny<TestCommand>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("PostProcessor failed"));

            var behavior = new RequestPostProcessorBehavior<TestCommand, string>(
                new[] { postProcessor.Object });

            var command = new TestCommand { Input = "test" };
            CommandHandlerDelegate<string> next = () => Task.FromResult("result");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await behavior.Handle(command, next, CancellationToken.None));

            Assert.Equal("PostProcessor failed", exception.Message);
        }

        [Fact]
        public async Task Handle_WhenHandlerThrows_PostProcessorsShouldNotBeCalled()
        {
            // Arrange
            var postProcessorCalled = false;
            var postProcessor = new Mock<IRequestPostProcessor<TestCommand, string>>();
            postProcessor.Setup(p => p.ProcessAsync(It.IsAny<TestCommand>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => postProcessorCalled = true)
                .Returns(Task.CompletedTask);

            var behavior = new RequestPostProcessorBehavior<TestCommand, string>(
                new[] { postProcessor.Object });

            var command = new TestCommand { Input = "test" };
            CommandHandlerDelegate<string> next = () => throw new InvalidOperationException("Handler failed");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await behavior.Handle(command, next, CancellationToken.None));

            Assert.False(postProcessorCalled);
        }
    }
}
