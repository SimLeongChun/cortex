using Cortex.Mediator.Behaviors.Transactional;
using Cortex.Mediator.Commands;
using Moq;
using System.Transactions;

namespace Cortex.Tests.Mediator.Transactional.Tests
{
    public class TransactionalCommandBehaviorTests
    {
        #region Test Commands

        public class TestCommand : ICommand<string>
        {
            public string Data { get; set; }
        }

        [NonTransactional]
        public class NonTransactionalTestCommand : ICommand<string>
        {
            public string Data { get; set; }
        }

        public class ExcludedTestCommand : ICommand<string>
        {
            public string Data { get; set; }
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TransactionalCommandBehavior<TestCommand, string>(null));
        }

        [Fact]
        public void Constructor_WithValidOptions_CreatesInstance()
        {
            // Arrange
            var options = new TransactionalOptions();

            // Act
            var behavior = new TransactionalCommandBehavior<TestCommand, string>(options);

            // Assert
            Assert.NotNull(behavior);
        }

        #endregion

        #region Handle Tests with TransactionScope

        [Fact]
        public async Task Handle_WithDefaultOptions_ExecutesCommandInTransaction()
        {
            // Arrange
            var options = new TransactionalOptions();
            var behavior = new TransactionalCommandBehavior<TestCommand, string>(options);
            var command = new TestCommand { Data = "test" };
            var expectedResult = "success";
            bool nextWasCalled = false;

            CommandHandlerDelegate<string> next = () =>
            {
                nextWasCalled = true;
                // Verify we're inside a transaction
                Assert.NotNull(Transaction.Current);
                return Task.FromResult(expectedResult);
            };

            // Act
            var result = await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.True(nextWasCalled);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task Handle_WhenNextThrowsException_DoesNotComplete()
        {
            // Arrange
            var options = new TransactionalOptions();
            var behavior = new TransactionalCommandBehavior<TestCommand, string>(options);
            var command = new TestCommand { Data = "test" };

            CommandHandlerDelegate<string> next = () =>
            {
                throw new InvalidOperationException("Test exception");
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => behavior.Handle(command, next, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_WithNonTransactionalAttribute_SkipsTransaction()
        {
            // Arrange
            var options = new TransactionalOptions();
            var behavior = new TransactionalCommandBehavior<NonTransactionalTestCommand, string>(options);
            var command = new NonTransactionalTestCommand { Data = "test" };
            var expectedResult = "success";
            bool nextWasCalled = false;

            CommandHandlerDelegate<string> next = () =>
            {
                nextWasCalled = true;
                // Verify we're NOT inside a transaction
                Assert.Null(Transaction.Current);
                return Task.FromResult(expectedResult);
            };

            // Act
            var result = await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.True(nextWasCalled);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task Handle_WithExcludedCommandType_SkipsTransaction()
        {
            // Arrange
            var options = new TransactionalOptions();
            options.ExcludeCommand<ExcludedTestCommand>();

            var behavior = new TransactionalCommandBehavior<ExcludedTestCommand, string>(options);
            var command = new ExcludedTestCommand { Data = "test" };
            var expectedResult = "success";
            bool nextWasCalled = false;

            CommandHandlerDelegate<string> next = () =>
            {
                nextWasCalled = true;
                // Verify we're NOT inside a transaction
                Assert.Null(Transaction.Current);
                return Task.FromResult(expectedResult);
            };

            // Act
            var result = await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.True(nextWasCalled);
            Assert.Equal(expectedResult, result);
        }

        #endregion

        #region Handle Tests with Custom TransactionalContext

        [Fact]
        public async Task Handle_WithCustomContext_UsesCustomTransaction()
        {
            // Arrange
            var options = new TransactionalOptions();
            var mockContext = new Mock<ITransactionalContext>();
            var behavior = new TransactionalCommandBehavior<TestCommand, string>(options, mockContext.Object);
            var command = new TestCommand { Data = "test" };
            var expectedResult = "success";

            mockContext.Setup(c => c.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
            mockContext.Setup(c => c.CommitAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

            CommandHandlerDelegate<string> next = () => Task.FromResult(expectedResult);

            // Act
            var result = await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, result);
            mockContext.Verify(c => c.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockContext.Verify(c => c.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockContext.Verify(c => c.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_WithCustomContext_WhenNextThrows_RollsBack()
        {
            // Arrange
            var options = new TransactionalOptions();
            var mockContext = new Mock<ITransactionalContext>();
            var behavior = new TransactionalCommandBehavior<TestCommand, string>(options, mockContext.Object);
            var command = new TestCommand { Data = "test" };

            mockContext.Setup(c => c.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
            mockContext.Setup(c => c.RollbackAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

            CommandHandlerDelegate<string> next = () =>
            {
                throw new InvalidOperationException("Test exception");
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => behavior.Handle(command, next, CancellationToken.None));

            mockContext.Verify(c => c.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockContext.Verify(c => c.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockContext.Verify(c => c.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_WithCustomContext_AndNonTransactionalAttribute_SkipsTransaction()
        {
            // Arrange
            var options = new TransactionalOptions();
            var mockContext = new Mock<ITransactionalContext>();
            var behavior = new TransactionalCommandBehavior<NonTransactionalTestCommand, string>(options, mockContext.Object);
            var command = new NonTransactionalTestCommand { Data = "test" };
            var expectedResult = "success";

            CommandHandlerDelegate<string> next = () => Task.FromResult(expectedResult);

            // Act
            var result = await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, result);
            mockContext.Verify(c => c.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
            mockContext.Verify(c => c.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region TransactionalOptions Tests

        [Fact]
        public async Task Handle_WithCustomIsolationLevel_UsesConfiguredLevel()
        {
            // Arrange
            var options = new TransactionalOptions
            {
                IsolationLevel = IsolationLevel.Serializable
            };
            var behavior = new TransactionalCommandBehavior<TestCommand, string>(options);
            var command = new TestCommand { Data = "test" };
            var expectedResult = "success";
            IsolationLevel? capturedLevel = null;

            CommandHandlerDelegate<string> next = () =>
            {
                capturedLevel = Transaction.Current?.IsolationLevel;
                return Task.FromResult(expectedResult);
            };

            // Act
            var result = await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, result);
            Assert.Equal(IsolationLevel.Serializable, capturedLevel);
        }

        #endregion
    }
}
