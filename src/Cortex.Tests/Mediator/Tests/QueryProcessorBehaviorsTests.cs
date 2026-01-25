using Cortex.Mediator.Processors;
using Cortex.Mediator.Queries;
using Moq;

namespace Cortex.Tests.Mediator.Tests
{
    public class QueryProcessorBehaviorsTests
    {
        public class TestQuery : IQuery<string>
        {
            public string Input { get; set; } = string.Empty;
        }

        [Fact]
        public async Task QueryPreProcessorBehavior_ShouldExecutePreProcessorsBeforeHandler()
        {
            // Arrange
            var executionOrder = new List<string>();

            var preProcessor = new Mock<IRequestPreProcessor<TestQuery>>();
            preProcessor.Setup(p => p.ProcessAsync(It.IsAny<TestQuery>(), It.IsAny<CancellationToken>()))
                .Callback(() => executionOrder.Add("PreProcessor"))
                .Returns(Task.CompletedTask);

            var behavior = new QueryPreProcessorBehavior<TestQuery, string>(
                new[] { preProcessor.Object });

            var query = new TestQuery { Input = "test" };
            QueryHandlerDelegate<string> next = () =>
            {
                executionOrder.Add("Handler");
                return Task.FromResult("result");
            };

            // Act
            var result = await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.Equal("result", result);
            Assert.Equal(2, executionOrder.Count);
            Assert.Equal("PreProcessor", executionOrder[0]);
            Assert.Equal("Handler", executionOrder[1]);
        }

        [Fact]
        public async Task QueryPostProcessorBehavior_ShouldExecutePostProcessorsAfterHandler()
        {
            // Arrange
            var executionOrder = new List<string>();

            var postProcessor = new Mock<IRequestPostProcessor<TestQuery, string>>();
            postProcessor.Setup(p => p.ProcessAsync(It.IsAny<TestQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => executionOrder.Add("PostProcessor"))
                .Returns(Task.CompletedTask);

            var behavior = new QueryPostProcessorBehavior<TestQuery, string>(
                new[] { postProcessor.Object });

            var query = new TestQuery { Input = "test" };
            QueryHandlerDelegate<string> next = () =>
            {
                executionOrder.Add("Handler");
                return Task.FromResult("result");
            };

            // Act
            var result = await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.Equal("result", result);
            Assert.Equal(2, executionOrder.Count);
            Assert.Equal("Handler", executionOrder[0]);
            Assert.Equal("PostProcessor", executionOrder[1]);
        }

        [Fact]
        public async Task QueryPostProcessorBehavior_ShouldPassQueryAndResponse()
        {
            // Arrange
            TestQuery capturedQuery = null!;
            string capturedResponse = null!;

            var postProcessor = new Mock<IRequestPostProcessor<TestQuery, string>>();
            postProcessor.Setup(p => p.ProcessAsync(It.IsAny<TestQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<TestQuery, string, CancellationToken>((q, r, ct) =>
                {
                    capturedQuery = q;
                    capturedResponse = r;
                })
                .Returns(Task.CompletedTask);

            var behavior = new QueryPostProcessorBehavior<TestQuery, string>(
                new[] { postProcessor.Object });

            var query = new TestQuery { Input = "test-input" };
            QueryHandlerDelegate<string> next = () => Task.FromResult("test-response");

            // Act
            await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedQuery);
            Assert.Equal("test-input", capturedQuery.Input);
            Assert.Equal("test-response", capturedResponse);
        }

        [Fact]
        public async Task QueryPreProcessorBehavior_WithNoProcessors_ShouldCallHandler()
        {
            // Arrange
            var behavior = new QueryPreProcessorBehavior<TestQuery, string>(
                Enumerable.Empty<IRequestPreProcessor<TestQuery>>());

            var query = new TestQuery { Input = "test" };
            QueryHandlerDelegate<string> next = () => Task.FromResult("result");

            // Act
            var result = await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.Equal("result", result);
        }

        [Fact]
        public async Task QueryPostProcessorBehavior_WithNoProcessors_ShouldReturnResult()
        {
            // Arrange
            var behavior = new QueryPostProcessorBehavior<TestQuery, string>(
                Enumerable.Empty<IRequestPostProcessor<TestQuery, string>>());

            var query = new TestQuery { Input = "test" };
            QueryHandlerDelegate<string> next = () => Task.FromResult("result");

            // Act
            var result = await behavior.Handle(query, next, CancellationToken.None);

            // Assert
            Assert.Equal("result", result);
        }
    }
}
