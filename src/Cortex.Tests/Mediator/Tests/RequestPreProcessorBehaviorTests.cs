using Cortex.Mediator.Commands;
using Cortex.Mediator.Processors;
using Moq;

namespace Cortex.Tests.Mediator.Tests
{
    public class RequestPreProcessorBehaviorTests
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
        public async Task Handle_ShouldExecuteAllPreProcessorsBeforeHandler()
        {
            // Arrange
            var executionOrder = new List<string>();

            var preProcessor1 = new Mock<IRequestPreProcessor<TestCommand>>();
            preProcessor1.Setup(p => p.ProcessAsync(It.IsAny<TestCommand>(), It.IsAny<CancellationToken>()))
                .Callback(() => executionOrder.Add("PreProcessor1"))
                .Returns(Task.CompletedTask);

            var preProcessor2 = new Mock<IRequestPreProcessor<TestCommand>>();
            preProcessor2.Setup(p => p.ProcessAsync(It.IsAny<TestCommand>(), It.IsAny<CancellationToken>()))
                .Callback(() => executionOrder.Add("PreProcessor2"))
                .Returns(Task.CompletedTask);

            var behavior = new RequestPreProcessorBehavior<TestCommand, string>(
                new[] { preProcessor1.Object, preProcessor2.Object });

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
            Assert.Equal("PreProcessor1", executionOrder[0]);
            Assert.Equal("PreProcessor2", executionOrder[1]);
            Assert.Equal("Handler", executionOrder[2]);
        }

        [Fact]
        public async Task Handle_WithNoPreProcessors_ShouldCallHandlerDirectly()
        {
            // Arrange
            var behavior = new RequestPreProcessorBehavior<TestCommand, string>(
                Enumerable.Empty<IRequestPreProcessor<TestCommand>>());

            var command = new TestCommand { Input = "test" };
            var handlerCalled = false;

            CommandHandlerDelegate<string> next = () =>
            {
                handlerCalled = true;
                return Task.FromResult("result");
            };

            // Act
            var result = await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.Equal("result", result);
            Assert.True(handlerCalled);
        }

        [Fact]
        public async Task Handle_VoidCommand_ShouldExecutePreProcessors()
        {
            // Arrange
            var executionOrder = new List<string>();

            var preProcessor = new Mock<IRequestPreProcessor<TestVoidCommand>>();
            preProcessor.Setup(p => p.ProcessAsync(It.IsAny<TestVoidCommand>(), It.IsAny<CancellationToken>()))
                .Callback(() => executionOrder.Add("PreProcessor"))
                .Returns(Task.CompletedTask);

            var behavior = new RequestPreProcessorBehavior<TestVoidCommand>(
                new[] { preProcessor.Object });

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
            Assert.Equal("PreProcessor", executionOrder[0]);
            Assert.Equal("Handler", executionOrder[1]);
        }

        [Fact]
        public async Task Handle_ShouldPassCommandToPreProcessors()
        {
            // Arrange
            TestCommand capturedCommand = null!;

            var preProcessor = new Mock<IRequestPreProcessor<TestCommand>>();
            preProcessor.Setup(p => p.ProcessAsync(It.IsAny<TestCommand>(), It.IsAny<CancellationToken>()))
                .Callback<TestCommand, CancellationToken>((cmd, ct) => capturedCommand = cmd)
                .Returns(Task.CompletedTask);

            var behavior = new RequestPreProcessorBehavior<TestCommand, string>(
                new[] { preProcessor.Object });

            var command = new TestCommand { Input = "test-input" };
            CommandHandlerDelegate<string> next = () => Task.FromResult("result");

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal("test-input", capturedCommand.Input);
        }

        [Fact]
        public async Task Handle_ShouldPassCancellationToken()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            CancellationToken capturedToken = default;

            var preProcessor = new Mock<IRequestPreProcessor<TestCommand>>();
            preProcessor.Setup(p => p.ProcessAsync(It.IsAny<TestCommand>(), It.IsAny<CancellationToken>()))
                .Callback<TestCommand, CancellationToken>((cmd, ct) => capturedToken = ct)
                .Returns(Task.CompletedTask);

            var behavior = new RequestPreProcessorBehavior<TestCommand, string>(
                new[] { preProcessor.Object });

            var command = new TestCommand { Input = "test" };
            CommandHandlerDelegate<string> next = () => Task.FromResult("result");

            // Act
            await behavior.Handle(command, next, cts.Token);

            // Assert
            Assert.Equal(cts.Token, capturedToken);
        }

        [Fact]
        public async Task Handle_WhenPreProcessorThrows_ShouldPropagateException()
        {
            // Arrange
            var preProcessor = new Mock<IRequestPreProcessor<TestCommand>>();
            preProcessor.Setup(p => p.ProcessAsync(It.IsAny<TestCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("PreProcessor failed"));

            var behavior = new RequestPreProcessorBehavior<TestCommand, string>(
                new[] { preProcessor.Object });

            var command = new TestCommand { Input = "test" };
            var handlerCalled = false;
            CommandHandlerDelegate<string> next = () =>
            {
                handlerCalled = true;
                return Task.FromResult("result");
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await behavior.Handle(command, next, CancellationToken.None));

            Assert.Equal("PreProcessor failed", exception.Message);
            Assert.False(handlerCalled); // Handler should not be called
        }
    }
}
