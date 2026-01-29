using Cortex.Mediator.Behaviors.Transactional;
using Cortex.Mediator.Commands;
using Moq;
using System.Transactions;

namespace Cortex.Tests.Mediator.Transactional.Tests
{
    public class TransactionalVoidCommandBehaviorTests
    {
        #region Test Commands

        public class TestVoidCommand : ICommand
        {
            public string Data { get; set; }
        }

        [NonTransactional]
        public class NonTransactionalVoidCommand : ICommand
        {
            public string Data { get; set; }
        }

        public class ExcludedVoidCommand : ICommand
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
                new TransactionalCommandBehavior<TestVoidCommand>(null));
        }

        [Fact]
        public void Constructor_WithValidOptions_CreatesInstance()
        {
            // Arrange
            var options = new TransactionalOptions();

            // Act
            var behavior = new TransactionalCommandBehavior<TestVoidCommand>(options);

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
            var behavior = new TransactionalCommandBehavior<TestVoidCommand>(options);
            var command = new TestVoidCommand { Data = "test" };
            bool nextWasCalled = false;

            CommandHandlerDelegate next = () =>
            {
                nextWasCalled = true;
                // Verify we're inside a transaction
                Assert.NotNull(Transaction.Current);
                return Task.CompletedTask;
            };

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.True(nextWasCalled);
        }

        [Fact]
        public async Task Handle_WhenNextThrowsException_DoesNotComplete()
        {
            // Arrange
            var options = new TransactionalOptions();
            var behavior = new TransactionalCommandBehavior<TestVoidCommand>(options);
            var command = new TestVoidCommand { Data = "test" };

            CommandHandlerDelegate next = () =>
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
            var behavior = new TransactionalCommandBehavior<NonTransactionalVoidCommand>(options);
            var command = new NonTransactionalVoidCommand { Data = "test" };
            bool nextWasCalled = false;

            CommandHandlerDelegate next = () =>
            {
                nextWasCalled = true;
                // Verify we're NOT inside a transaction
                Assert.Null(Transaction.Current);
                return Task.CompletedTask;
            };

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.True(nextWasCalled);
        }

        [Fact]
        public async Task Handle_WithExcludedCommandType_SkipsTransaction()
        {
            // Arrange
            var options = new TransactionalOptions();
            options.ExcludeCommand<ExcludedVoidCommand>();

            var behavior = new TransactionalCommandBehavior<ExcludedVoidCommand>(options);
            var command = new ExcludedVoidCommand { Data = "test" };
            bool nextWasCalled = false;

            CommandHandlerDelegate next = () =>
            {
                nextWasCalled = true;
                // Verify we're NOT inside a transaction
                Assert.Null(Transaction.Current);
                return Task.CompletedTask;
            };

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.True(nextWasCalled);
        }

        #endregion

        #region Handle Tests with Custom TransactionalContext

        [Fact]
        public async Task Handle_WithCustomContext_UsesCustomTransaction()
        {
            // Arrange
            var options = new TransactionalOptions();
            var mockContext = new Mock<ITransactionalContext>();
            var behavior = new TransactionalCommandBehavior<TestVoidCommand>(options, mockContext.Object);
            var command = new TestVoidCommand { Data = "test" };

            mockContext.Setup(c => c.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
            mockContext.Setup(c => c.CommitAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

            CommandHandlerDelegate next = () => Task.CompletedTask;

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
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
            var behavior = new TransactionalCommandBehavior<TestVoidCommand>(options, mockContext.Object);
            var command = new TestVoidCommand { Data = "test" };

            mockContext.Setup(c => c.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
            mockContext.Setup(c => c.RollbackAsync(It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

            CommandHandlerDelegate next = () =>
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
            var behavior = new TransactionalCommandBehavior<NonTransactionalVoidCommand>(options, mockContext.Object);
            var command = new NonTransactionalVoidCommand { Data = "test" };

            CommandHandlerDelegate next = () => Task.CompletedTask;

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
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
            var behavior = new TransactionalCommandBehavior<TestVoidCommand>(options);
            var command = new TestVoidCommand { Data = "test" };
            IsolationLevel? capturedLevel = null;

            CommandHandlerDelegate next = () =>
            {
                capturedLevel = Transaction.Current?.IsolationLevel;
                return Task.CompletedTask;
            };

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.Equal(IsolationLevel.Serializable, capturedLevel);
        }

        [Fact]
        public async Task Handle_WithExcludeCommandsMethod_SkipsTransaction()
        {
            // Arrange
            var options = new TransactionalOptions()
                .ExcludeCommands(typeof(ExcludedVoidCommand), typeof(TestVoidCommand));

            var behavior = new TransactionalCommandBehavior<TestVoidCommand>(options);
            var command = new TestVoidCommand { Data = "test" };
            bool nextWasCalled = false;

            CommandHandlerDelegate next = () =>
            {
                nextWasCalled = true;
                Assert.Null(Transaction.Current);
                return Task.CompletedTask;
            };

            // Act
            await behavior.Handle(command, next, CancellationToken.None);

            // Assert
            Assert.True(nextWasCalled);
        }

        #endregion
    }
}
