using Cortex.Types;

namespace Cortex.Tests.Types.Tests
{
    public class ResultExtensionsTests
    {
        #region Static Factory Methods Tests

        [Fact]
        public void Success_CreatesSuccessfulResult()
        {
            // Act
            var result = Result.Success(42);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void Success_WithCustomErrorType_CreatesSuccessfulResult()
        {
            // Act
            var result = Result.Success<int, string>(42);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void Failure_WithError_CreatesFailedResult()
        {
            // Arrange
            var error = new ResultError("Test error");

            // Act
            var result = Result.Failure<int>(error);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(error, result.Error);
        }

        [Fact]
        public void Failure_WithMessage_CreatesFailedResult()
        {
            // Act
            var result = Result.Failure<int>("Test error");

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Test error", result.Error.Message);
        }

        [Fact]
        public void Failure_WithException_CreatesFailedResult()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = Result.Failure<int>(exception);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Test exception", result.Error.Message);
            Assert.Same(exception, result.Error.Exception);
        }

        [Fact]
        public void Failure_WithCustomErrorType_CreatesFailedResult()
        {
            // Act
            var result = Result.Failure<int, string>("Custom error");

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Custom error", result.Error);
        }

        #endregion

        #region Try Tests

        [Fact]
        public void Try_WhenFunctionSucceeds_ReturnsSuccessResult()
        {
            // Act
            var result = Result.Try(() => 42);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void Try_WhenFunctionThrows_ReturnsFailureResult()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = Result.Try<int>(() => throw exception);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Test exception", result.Error.Message);
            Assert.Same(exception, result.Error.Exception);
        }

        [Fact]
        public void Try_WithExceptionHandler_WhenFunctionSucceeds_ReturnsSuccessResult()
        {
            // Act
            var result = Result.Try(
                () => 42,
                ex => new ResultError($"Handled: {ex.Message}"));

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void Try_WithExceptionHandler_WhenFunctionThrows_UsesHandler()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = Result.Try<int>(
                () => throw exception,
                ex => new ResultError($"Handled: {ex.Message}", "HANDLED"));

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Handled: Test exception", result.Error.Message);
            Assert.Equal("HANDLED", result.Error.Code);
        }

        #endregion

        #region TryAsync Tests

        [Fact]
        public async Task TryAsync_WhenFunctionSucceeds_ReturnsSuccessResult()
        {
            // Act
            var result = await Result.TryAsync(async () =>
            {
                await Task.Delay(1);
                return 42;
            });

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public async Task TryAsync_WhenFunctionThrows_ReturnsFailureResult()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = await Result.TryAsync<int>(async () =>
            {
                await Task.Delay(1);
                throw exception;
            });

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Test exception", result.Error.Message);
            Assert.Same(exception, result.Error.Exception);
        }

        #endregion

        #region Combine Tests

        [Fact]
        public void Combine_TwoSuccessResults_ReturnsCombinedSuccess()
        {
            // Arrange
            var result1 = Result.Success(42);
            var result2 = Result.Success("test");

            // Act
            var combined = Result.Combine(result1, result2);

            // Assert
            Assert.True(combined.IsSuccess);
            Assert.Equal((42, "test"), combined.Value);
        }

        [Fact]
        public void Combine_FirstResultFails_ReturnsFirstError()
        {
            // Arrange
            var error = new ResultError("First error");
            var result1 = Result.Failure<int>(error);
            var result2 = Result.Success("test");

            // Act
            var combined = Result.Combine(result1, result2);

            // Assert
            Assert.True(combined.IsFailure);
            Assert.Equal(error, combined.Error);
        }

        [Fact]
        public void Combine_SecondResultFails_ReturnsSecondError()
        {
            // Arrange
            var result1 = Result.Success(42);
            var error = new ResultError("Second error");
            var result2 = Result.Failure<string>(error);

            // Act
            var combined = Result.Combine(result1, result2);

            // Assert
            Assert.True(combined.IsFailure);
            Assert.Equal(error, combined.Error);
        }

        [Fact]
        public void Combine_BothResultsFail_ReturnsFirstError()
        {
            // Arrange
            var error1 = new ResultError("First error");
            var error2 = new ResultError("Second error");
            var result1 = Result.Failure<int>(error1);
            var result2 = Result.Failure<string>(error2);

            // Act
            var combined = Result.Combine(result1, result2);

            // Assert
            Assert.True(combined.IsFailure);
            Assert.Equal(error1, combined.Error);
        }

        [Fact]
        public void Combine_ThreeSuccessResults_ReturnsCombinedSuccess()
        {
            // Arrange
            var result1 = Result.Success(42);
            var result2 = Result.Success("test");
            var result3 = Result.Success(3.14);

            // Act
            var combined = Result.Combine(result1, result2, result3);

            // Assert
            Assert.True(combined.IsSuccess);
            Assert.Equal((42, "test", 3.14), combined.Value);
        }

        [Fact]
        public void Combine_ThirdResultFails_ReturnsThirdError()
        {
            // Arrange
            var result1 = Result.Success(42);
            var result2 = Result.Success("test");
            var error = new ResultError("Third error");
            var result3 = Result.Failure<double>(error);

            // Act
            var combined = Result.Combine(result1, result2, result3);

            // Assert
            Assert.True(combined.IsFailure);
            Assert.Equal(error, combined.Error);
        }

        #endregion

        #region SuccessIf Tests

        [Fact]
        public void SuccessIf_WhenConditionIsTrue_ReturnsSuccess()
        {
            // Act
            var result = Result.SuccessIf(true, 42, "Should not see this");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void SuccessIf_WhenConditionIsFalse_ReturnsFailure()
        {
            // Act
            var result = Result.SuccessIf(false, 42, "Condition failed");

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Condition failed", result.Error.Message);
        }

        [Fact]
        public void SuccessIf_WithError_WhenConditionIsTrue_ReturnsSuccess()
        {
            // Arrange
            var error = new ResultError("Should not see this");

            // Act
            var result = Result.SuccessIf(true, 42, error);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void SuccessIf_WithError_WhenConditionIsFalse_ReturnsFailure()
        {
            // Arrange
            var error = new ResultError("Condition failed", "COND_FAIL");

            // Act
            var result = Result.SuccessIf(false, 42, error);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(error, result.Error);
        }

        #endregion

        #region FailureIf Tests

        [Fact]
        public void FailureIf_WhenConditionIsTrue_ReturnsFailure()
        {
            // Act
            var result = Result.FailureIf(true, 42, "Condition triggered failure");

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Condition triggered failure", result.Error.Message);
        }

        [Fact]
        public void FailureIf_WhenConditionIsFalse_ReturnsSuccess()
        {
            // Act
            var result = Result.FailureIf(false, 42, "Should not see this");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void FailureIf_WithError_WhenConditionIsTrue_ReturnsFailure()
        {
            // Arrange
            var error = new ResultError("Condition triggered failure", "COND_FAIL");

            // Act
            var result = Result.FailureIf(true, 42, error);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(error, result.Error);
        }

        [Fact]
        public void FailureIf_WithError_WhenConditionIsFalse_ReturnsSuccess()
        {
            // Arrange
            var error = new ResultError("Should not see this");

            // Act
            var result = Result.FailureIf(false, 42, error);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        #endregion

        #region Real-World Scenario Tests

        [Fact]
        public void RealWorldScenario_ValidationChain()
        {
            // Arrange
            string? username = "john_doe";

            // Act
            var result = Result.Success(username)
                .Ensure(u => !string.IsNullOrEmpty(u), "Username cannot be empty")
                .Ensure(u => u!.Length >= 3, "Username must be at least 3 characters")
                .Ensure(u => u!.Length <= 20, "Username must be at most 20 characters")
                .Map(u => u!.ToUpperInvariant());

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("JOHN_DOE", result.Value);
        }

        [Fact]
        public void RealWorldScenario_ValidationChainFails()
        {
            // Arrange
            string? username = "ab";

            // Act
            var result = Result.Success(username)
                .Ensure(u => !string.IsNullOrEmpty(u), "Username cannot be empty")
                .Ensure(u => u!.Length >= 3, "Username must be at least 3 characters")
                .Ensure(u => u!.Length <= 20, "Username must be at most 20 characters")
                .Map(u => u!.ToUpperInvariant());

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("Username must be at least 3 characters", result.Error.Message);
        }

        [Fact]
        public void RealWorldScenario_ChainedOperations()
        {
            // Arrange
            int ParseNumber(string s) => int.Parse(s);
            int Double(int n) => n * 2;

            // Act
            var result = Result.Try(() => ParseNumber("21"))
                .Map(Double)
                .Map(n => $"Result: {n}");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("Result: 42", result.Value);
        }

        [Fact]
        public void RealWorldScenario_ChainedOperationsWithFailure()
        {
            // Arrange
            int ParseNumber(string s) => int.Parse(s);
            int Double(int n) => n * 2;

            // Act
            var result = Result.Try(() => ParseNumber("not a number"))
                .Map(Double)
                .Map(n => $"Result: {n}");

            // Assert
            Assert.True(result.IsFailure);
            Assert.NotNull(result.Error.Exception);
        }

        [Fact]
        public async Task RealWorldScenario_AsyncOperations()
        {
            // Arrange
            async Task<int> FetchDataAsync()
            {
                await Task.Delay(1);
                return 42;
            }

            // Act
            var result = await Result.TryAsync(FetchDataAsync);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
        }

        #endregion
    }
}
