using Cortex.Mediator.Behaviors.Transactional;
using System.Transactions;

namespace Cortex.Tests.Mediator.Transactional.Tests
{
    public class TransactionalOptionsTests
    {
        #region Default Values Tests

        [Fact]
        public void DefaultOptions_HasReadCommittedIsolationLevel()
        {
            // Arrange & Act
            var options = new TransactionalOptions();

            // Assert
            Assert.Equal(IsolationLevel.ReadCommitted, options.IsolationLevel);
        }

        [Fact]
        public void DefaultOptions_Has30SecondTimeout()
        {
            // Arrange & Act
            var options = new TransactionalOptions();

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
        }

        [Fact]
        public void DefaultOptions_HasRequiredScopeOption()
        {
            // Arrange & Act
            var options = new TransactionalOptions();

            // Assert
            Assert.Equal(TransactionScopeOption.Required, options.ScopeOption);
        }

        [Fact]
        public void DefaultOptions_HasAsyncFlowEnabled()
        {
            // Arrange & Act
            var options = new TransactionalOptions();

            // Assert
            Assert.Equal(TransactionScopeAsyncFlowOption.Enabled, options.AsyncFlowOption);
        }

        [Fact]
        public void DefaultOptions_HasEmptyExcludedCommandTypes()
        {
            // Arrange & Act
            var options = new TransactionalOptions();

            // Assert
            Assert.NotNull(options.ExcludedCommandTypes);
            Assert.Empty(options.ExcludedCommandTypes);
        }

        [Fact]
        public void Default_ReturnsNewInstanceWithDefaults()
        {
            // Arrange & Act
            var options = TransactionalOptions.Default;

            // Assert
            Assert.NotNull(options);
            Assert.Equal(IsolationLevel.ReadCommitted, options.IsolationLevel);
            Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
        }

        #endregion

        #region ExcludeCommand Tests

        public class TestCommand1 { }
        public class TestCommand2 { }
        public class TestCommand3 { }

        [Fact]
        public void ExcludeCommand_AddsTypeToExcludedList()
        {
            // Arrange
            var options = new TransactionalOptions();

            // Act
            options.ExcludeCommand<TestCommand1>();

            // Assert
            Assert.Contains(typeof(TestCommand1), options.ExcludedCommandTypes);
        }

        [Fact]
        public void ExcludeCommand_ReturnsSameInstance_ForFluent()
        {
            // Arrange
            var options = new TransactionalOptions();

            // Act
            var result = options.ExcludeCommand<TestCommand1>();

            // Assert
            Assert.Same(options, result);
        }

        [Fact]
        public void ExcludeCommand_CanChainMultipleCalls()
        {
            // Arrange & Act
            var options = new TransactionalOptions()
                .ExcludeCommand<TestCommand1>()
                .ExcludeCommand<TestCommand2>();

            // Assert
            Assert.Contains(typeof(TestCommand1), options.ExcludedCommandTypes);
            Assert.Contains(typeof(TestCommand2), options.ExcludedCommandTypes);
            Assert.Equal(2, options.ExcludedCommandTypes.Count);
        }

        #endregion

        #region ExcludeCommands Tests

        [Fact]
        public void ExcludeCommands_AddsMultipleTypesToExcludedList()
        {
            // Arrange
            var options = new TransactionalOptions();

            // Act
            options.ExcludeCommands(typeof(TestCommand1), typeof(TestCommand2), typeof(TestCommand3));

            // Assert
            Assert.Contains(typeof(TestCommand1), options.ExcludedCommandTypes);
            Assert.Contains(typeof(TestCommand2), options.ExcludedCommandTypes);
            Assert.Contains(typeof(TestCommand3), options.ExcludedCommandTypes);
            Assert.Equal(3, options.ExcludedCommandTypes.Count);
        }

        [Fact]
        public void ExcludeCommands_ReturnsSameInstance_ForFluent()
        {
            // Arrange
            var options = new TransactionalOptions();

            // Act
            var result = options.ExcludeCommands(typeof(TestCommand1));

            // Assert
            Assert.Same(options, result);
        }

        [Fact]
        public void ExcludeCommands_WithEmptyArray_DoesNotThrow()
        {
            // Arrange
            var options = new TransactionalOptions();

            // Act & Assert (should not throw)
            var result = options.ExcludeCommands();

            Assert.Empty(options.ExcludedCommandTypes);
        }

        [Fact]
        public void ExcludeCommands_DuplicateType_DoesNotAddTwice()
        {
            // Arrange
            var options = new TransactionalOptions();

            // Act
            options.ExcludeCommands(typeof(TestCommand1), typeof(TestCommand1), typeof(TestCommand1));

            // Assert - HashSet ensures no duplicates
            Assert.Single(options.ExcludedCommandTypes);
        }

        #endregion

        #region Configuration Combination Tests

        [Fact]
        public void Options_CanCombineMultipleSettings()
        {
            // Arrange & Act
            var options = new TransactionalOptions
            {
                IsolationLevel = IsolationLevel.Serializable,
                Timeout = TimeSpan.FromMinutes(5),
                ScopeOption = TransactionScopeOption.RequiresNew,
                AsyncFlowOption = TransactionScopeAsyncFlowOption.Suppress
            };
            options.ExcludeCommand<TestCommand1>()
                   .ExcludeCommand<TestCommand2>();

            // Assert
            Assert.Equal(IsolationLevel.Serializable, options.IsolationLevel);
            Assert.Equal(TimeSpan.FromMinutes(5), options.Timeout);
            Assert.Equal(TransactionScopeOption.RequiresNew, options.ScopeOption);
            Assert.Equal(TransactionScopeAsyncFlowOption.Suppress, options.AsyncFlowOption);
            Assert.Equal(2, options.ExcludedCommandTypes.Count);
        }

        #endregion
    }
}
