using Cortex.Types;

namespace Cortex.Tests.Types.Tests
{
    public class Result2Tests
    {
        #region Custom Error Type for Testing

        private record TestError(string Code, string Description);

        #endregion

        #region Creation Tests

        [Fact]
        public void Success_CreatesSuccessfulResult()
        {
            // Act
            var result = Result<int, TestError>.Success(42);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void Failure_CreatesFailedResult()
        {
            // Arrange
            var error = new TestError("ERR001", "Test error");

            // Act
            var result = Result<int, TestError>.Failure(error);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.True(result.IsFailure);
            Assert.Equal(error, result.Error);
        }

        #endregion

        #region Implicit Conversion Tests

        [Fact]
        public void ImplicitConversion_FromValue_CreatesSuccessResult()
        {
            // Act
            Result<string, TestError> result = "test value";

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("test value", result.Value);
        }

        #endregion

        #region Value Access Tests

        [Fact]
        public void Value_OnSuccess_ReturnsValue()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);

            // Act & Assert
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void Value_OnFailure_ThrowsInvalidOperationException()
        {
            // Arrange
            var result = Result<int, TestError>.Failure(new TestError("ERR", "Error"));

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => result.Value);
            Assert.Contains("Cannot access Value", exception.Message);
        }

        [Fact]
        public void Error_OnFailure_ReturnsError()
        {
            // Arrange
            var error = new TestError("ERR001", "Test error");
            var result = Result<int, TestError>.Failure(error);

            // Act & Assert
            Assert.Equal(error, result.Error);
        }

        [Fact]
        public void Error_OnSuccess_ThrowsInvalidOperationException()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => result.Error);
            Assert.Contains("Cannot access Error", exception.Message);
        }

        #endregion

        #region TryGet Tests

        [Fact]
        public void TryGetValue_OnSuccess_ReturnsTrueAndValue()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);

            // Act
            var success = result.TryGetValue(out var value);

            // Assert
            Assert.True(success);
            Assert.Equal(42, value);
        }

        [Fact]
        public void TryGetValue_OnFailure_ReturnsFalse()
        {
            // Arrange
            var result = Result<int, TestError>.Failure(new TestError("ERR", "Error"));

            // Act
            var success = result.TryGetValue(out var value);

            // Assert
            Assert.False(success);
            Assert.Equal(default, value);
        }

        [Fact]
        public void TryGetError_OnFailure_ReturnsTrueAndError()
        {
            // Arrange
            var error = new TestError("ERR001", "Test error");
            var result = Result<int, TestError>.Failure(error);

            // Act
            var hasError = result.TryGetError(out var retrievedError);

            // Assert
            Assert.True(hasError);
            Assert.Equal(error, retrievedError);
        }

        [Fact]
        public void TryGetError_OnSuccess_ReturnsFalse()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);

            // Act
            var hasError = result.TryGetError(out var error);

            // Assert
            Assert.False(hasError);
            Assert.Null(error);
        }

        #endregion

        #region GetValueOrDefault Tests

        [Fact]
        public void GetValueOrDefault_OnSuccess_ReturnsValue()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);

            // Act
            var value = result.GetValueOrDefault(0);

            // Assert
            Assert.Equal(42, value);
        }

        [Fact]
        public void GetValueOrDefault_OnFailure_ReturnsDefault()
        {
            // Arrange
            var result = Result<int, TestError>.Failure(new TestError("ERR", "Error"));

            // Act
            var value = result.GetValueOrDefault(99);

            // Assert
            Assert.Equal(99, value);
        }

        [Fact]
        public void GetValueOrDefault_WithFactory_OnFailure_CallsFactory()
        {
            // Arrange
            var result = Result<int, TestError>.Failure(new TestError("ERR", "Error"));

            // Act
            var value = result.GetValueOrDefault(() => 99);

            // Assert
            Assert.Equal(99, value);
        }

        [Fact]
        public void GetValueOrDefault_WithErrorHandler_OnFailure_PassesError()
        {
            // Arrange
            var error = new TestError("ERR001", "Test error");
            var result = Result<string, TestError>.Failure(error);
            TestError? capturedError = null;

            // Act
            var value = result.GetValueOrDefault(e => { capturedError = e; return "default"; });

            // Assert
            Assert.Equal("default", value);
            Assert.Equal(error, capturedError);
        }

        #endregion

        #region Match Tests

        [Fact]
        public void Match_OnSuccess_ExecutesSuccessHandler()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);

            // Act
            var output = result.Match(
                onSuccess: v => $"Success: {v}",
                onFailure: e => $"Failure: {e.Code}");

            // Assert
            Assert.Equal("Success: 42", output);
        }

        [Fact]
        public void Match_OnFailure_ExecutesFailureHandler()
        {
            // Arrange
            var result = Result<int, TestError>.Failure(new TestError("ERR001", "Error"));

            // Act
            var output = result.Match(
                onSuccess: v => $"Success: {v}",
                onFailure: e => $"Failure: {e.Code}");

            // Assert
            Assert.Equal("Failure: ERR001", output);
        }

        #endregion

        #region Switch Tests

        [Fact]
        public void Switch_OnSuccess_ExecutesSuccessAction()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);
            int? capturedValue = null;
            TestError? capturedError = null;

            // Act
            result.Switch(
                onSuccess: v => capturedValue = v,
                onFailure: e => capturedError = e);

            // Assert
            Assert.Equal(42, capturedValue);
            Assert.Null(capturedError);
        }

        [Fact]
        public void Switch_OnFailure_ExecutesFailureAction()
        {
            // Arrange
            var error = new TestError("ERR001", "Test error");
            var result = Result<int, TestError>.Failure(error);
            int? capturedValue = null;
            TestError? capturedError = null;

            // Act
            result.Switch(
                onSuccess: v => capturedValue = v,
                onFailure: e => capturedError = e);

            // Assert
            Assert.Null(capturedValue);
            Assert.Equal(error, capturedError);
        }

        #endregion

        #region Map Tests

        [Fact]
        public void Map_OnSuccess_TransformsValue()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);

            // Act
            var mapped = result.Map(v => v.ToString());

            // Assert
            Assert.True(mapped.IsSuccess);
            Assert.Equal("42", mapped.Value);
        }

        [Fact]
        public void Map_OnFailure_PreservesError()
        {
            // Arrange
            var error = new TestError("ERR001", "Test error");
            var result = Result<int, TestError>.Failure(error);

            // Act
            var mapped = result.Map(v => v.ToString());

            // Assert
            Assert.True(mapped.IsFailure);
            Assert.Equal(error, mapped.Error);
        }

        [Fact]
        public void MapError_OnFailure_TransformsError()
        {
            // Arrange
            var result = Result<int, TestError>.Failure(new TestError("ERR001", "Original"));

            // Act
            var mapped = result.MapError(e => new TestError(e.Code, $"Mapped: {e.Description}"));

            // Assert
            Assert.True(mapped.IsFailure);
            Assert.Equal("Mapped: Original", mapped.Error.Description);
        }

        [Fact]
        public void MapError_CanChangeErrorType()
        {
            // Arrange
            var result = Result<int, TestError>.Failure(new TestError("ERR001", "Test"));

            // Act
            var mapped = result.MapError(e => e.Code); // Transform to string error

            // Assert
            Assert.True(mapped.IsFailure);
            Assert.Equal("ERR001", mapped.Error);
        }

        [Fact]
        public void MapError_OnSuccess_PreservesValue()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);

            // Act
            var mapped = result.MapError(e => new TestError("NEW", "Should not happen"));

            // Assert
            Assert.True(mapped.IsSuccess);
            Assert.Equal(42, mapped.Value);
        }

        #endregion

        #region Bind Tests

        [Fact]
        public void Bind_OnSuccess_ChainsOperation()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);

            // Act
            var bound = result.Bind(v => Result<string, TestError>.Success($"Value: {v}"));

            // Assert
            Assert.True(bound.IsSuccess);
            Assert.Equal("Value: 42", bound.Value);
        }

        [Fact]
        public void Bind_OnSuccess_CanReturnFailure()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);
            var error = new TestError("VAL001", "Validation failed");

            // Act
            var bound = result.Bind(v => Result<string, TestError>.Failure(error));

            // Assert
            Assert.True(bound.IsFailure);
            Assert.Equal(error, bound.Error);
        }

        [Fact]
        public void Bind_OnFailure_SkipsOperation()
        {
            // Arrange
            var error = new TestError("ERR001", "Original error");
            var result = Result<int, TestError>.Failure(error);
            var operationCalled = false;

            // Act
            var bound = result.Bind(v => { operationCalled = true; return Result<string, TestError>.Success("test"); });

            // Assert
            Assert.True(bound.IsFailure);
            Assert.Equal(error, bound.Error);
            Assert.False(operationCalled);
        }

        #endregion

        #region Tap Tests

        [Fact]
        public void Tap_OnSuccess_ExecutesAction()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);
            int? capturedValue = null;

            // Act
            var tapped = result.Tap(v => capturedValue = v);

            // Assert
            Assert.Equal(42, capturedValue);
            Assert.Equal(result, tapped);
        }

        [Fact]
        public void Tap_OnFailure_SkipsAction()
        {
            // Arrange
            var result = Result<int, TestError>.Failure(new TestError("ERR", "Error"));
            var actionCalled = false;

            // Act
            var tapped = result.Tap(v => actionCalled = true);

            // Assert
            Assert.False(actionCalled);
        }

        [Fact]
        public void TapError_OnFailure_ExecutesAction()
        {
            // Arrange
            var error = new TestError("ERR001", "Test error");
            var result = Result<int, TestError>.Failure(error);
            TestError? capturedError = null;

            // Act
            var tapped = result.TapError(e => capturedError = e);

            // Assert
            Assert.Equal(error, capturedError);
        }

        [Fact]
        public void TapError_OnSuccess_SkipsAction()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);
            var actionCalled = false;

            // Act
            var tapped = result.TapError(e => actionCalled = true);

            // Assert
            Assert.False(actionCalled);
        }

        #endregion

        #region Ensure Tests

        [Fact]
        public void Ensure_WhenPredicatePasses_ReturnsOriginalResult()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);

            // Act
            var ensured = result.Ensure(v => v > 0, new TestError("VAL001", "Must be positive"));

            // Assert
            Assert.True(ensured.IsSuccess);
            Assert.Equal(42, ensured.Value);
        }

        [Fact]
        public void Ensure_WhenPredicateFails_ReturnsFailure()
        {
            // Arrange
            var result = Result<int, TestError>.Success(-5);
            var error = new TestError("VAL001", "Must be positive");

            // Act
            var ensured = result.Ensure(v => v > 0, error);

            // Assert
            Assert.True(ensured.IsFailure);
            Assert.Equal(error, ensured.Error);
        }

        [Fact]
        public void Ensure_OnFailure_SkipsPredicate()
        {
            // Arrange
            var originalError = new TestError("ERR001", "Original error");
            var result = Result<int, TestError>.Failure(originalError);
            var predicateCalled = false;

            // Act
            var ensured = result.Ensure(v => { predicateCalled = true; return v > 0; }, new TestError("NEW", "New"));

            // Assert
            Assert.True(ensured.IsFailure);
            Assert.Equal(originalError, ensured.Error);
            Assert.False(predicateCalled);
        }

        #endregion

        #region ToResult Tests

        [Fact]
        public void ToResult_OnSuccess_ConvertsToBuiltInResult()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);

            // Act
            var converted = result.ToResult(e => new ResultError(e.Description, e.Code));

            // Assert
            Assert.True(converted.IsSuccess);
            Assert.Equal(42, converted.Value);
        }

        [Fact]
        public void ToResult_OnFailure_ConvertsErrorToResultError()
        {
            // Arrange
            var error = new TestError("ERR001", "Test error");
            var result = Result<int, TestError>.Failure(error);

            // Act
            var converted = result.ToResult(e => new ResultError(e.Description, e.Code));

            // Assert
            Assert.True(converted.IsFailure);
            Assert.Equal("Test error", converted.Error.Message);
            Assert.Equal("ERR001", converted.Error.Code);
        }

        #endregion

        #region Equality Tests

        [Fact]
        public void Equals_SuccessResultsWithSameValue_ReturnsTrue()
        {
            // Arrange
            var result1 = Result<int, TestError>.Success(42);
            var result2 = Result<int, TestError>.Success(42);

            // Act & Assert
            Assert.Equal(result1, result2);
            Assert.True(result1 == result2);
            Assert.False(result1 != result2);
        }

        [Fact]
        public void Equals_SuccessResultsWithDifferentValues_ReturnsFalse()
        {
            // Arrange
            var result1 = Result<int, TestError>.Success(42);
            var result2 = Result<int, TestError>.Success(99);

            // Act & Assert
            Assert.NotEqual(result1, result2);
        }

        [Fact]
        public void Equals_FailureResultsWithSameError_ReturnsTrue()
        {
            // Arrange
            var error = new TestError("ERR001", "Test error");
            var result1 = Result<int, TestError>.Failure(error);
            var result2 = Result<int, TestError>.Failure(error);

            // Act & Assert
            Assert.Equal(result1, result2);
        }

        [Fact]
        public void Equals_SuccessAndFailure_ReturnsFalse()
        {
            // Arrange
            var success = Result<int, TestError>.Success(42);
            var failure = Result<int, TestError>.Failure(new TestError("ERR", "Error"));

            // Act & Assert
            Assert.NotEqual(success, failure);
        }

        [Fact]
        public void GetHashCode_SameResults_ReturnsSameHashCode()
        {
            // Arrange
            var result1 = Result<int, TestError>.Success(42);
            var result2 = Result<int, TestError>.Success(42);

            // Act & Assert
            Assert.Equal(result1.GetHashCode(), result2.GetHashCode());
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_OnSuccess_ReturnsFormattedString()
        {
            // Arrange
            var result = Result<int, TestError>.Success(42);

            // Act
            var str = result.ToString();

            // Assert
            Assert.Equal("Success(42)", str);
        }

        [Fact]
        public void ToString_OnFailure_ReturnsFormattedString()
        {
            // Arrange
            var error = new TestError("ERR001", "Test error");
            var result = Result<int, TestError>.Failure(error);

            // Act
            var str = result.ToString();

            // Assert
            Assert.Contains("Failure", str);
        }

        #endregion
    }
}
