using Cortex.Types;

namespace Cortex.Tests.Types.Tests
{
    public class ResultErrorTests
    {
        [Fact]
        public void Constructor_WithMessage_SetsMessageProperty()
        {
            // Arrange & Act
            var error = new ResultError("Test error");

            // Assert
            Assert.Equal("Test error", error.Message);
            Assert.Null(error.Code);
            Assert.Null(error.Exception);
            Assert.Empty(error.Metadata);
        }

        [Fact]
        public void Constructor_WithMessageAndCode_SetsBothProperties()
        {
            // Arrange & Act
            var error = new ResultError("Test error", "ERR001");

            // Assert
            Assert.Equal("Test error", error.Message);
            Assert.Equal("ERR001", error.Code);
            Assert.Null(error.Exception);
        }

        [Fact]
        public void Constructor_WithMessageAndException_SetsBothProperties()
        {
            // Arrange
            var exception = new InvalidOperationException("Inner exception");

            // Act
            var error = new ResultError("Test error", exception);

            // Assert
            Assert.Equal("Test error", error.Message);
            Assert.Null(error.Code);
            Assert.Same(exception, error.Exception);
        }

        [Fact]
        public void Constructor_WithAllParameters_SetsAllProperties()
        {
            // Arrange
            var exception = new InvalidOperationException("Inner exception");
            var metadata = new Dictionary<string, object> { ["key"] = "value" };

            // Act
            var error = new ResultError("Test error", "ERR001", exception, metadata);

            // Assert
            Assert.Equal("Test error", error.Message);
            Assert.Equal("ERR001", error.Code);
            Assert.Same(exception, error.Exception);
            Assert.Equal("value", error.Metadata["key"]);
        }

        [Fact]
        public void Constructor_WithNullMessage_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ResultError(null!));
        }

        [Fact]
        public void FromException_CreatesErrorFromException()
        {
            // Arrange
            var exception = new ArgumentException("Argument error");

            // Act
            var error = ResultError.FromException(exception);

            // Assert
            Assert.Equal("Argument error", error.Message);
            Assert.Equal("ArgumentException", error.Code);
            Assert.Same(exception, error.Exception);
        }

        [Fact]
        public void FromException_WithNullException_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ResultError.FromException(null!));
        }

        [Fact]
        public void Aggregate_WithSingleError_ReturnsSameError()
        {
            // Arrange
            var error = new ResultError("Single error");

            // Act
            var result = ResultError.Aggregate(new[] { error });

            // Assert
            Assert.Same(error, result);
        }

        [Fact]
        public void Aggregate_WithMultipleErrors_CreatesCompositeError()
        {
            // Arrange
            var error1 = new ResultError("Error 1");
            var error2 = new ResultError("Error 2");

            // Act
            var result = ResultError.Aggregate(new[] { error1, error2 });

            // Assert
            Assert.Contains("Error 1", result.Message);
            Assert.Contains("Error 2", result.Message);
            Assert.Equal("AGGREGATE_ERROR", result.Code);
            Assert.True(result.Metadata.ContainsKey("InnerErrors"));
        }

        [Fact]
        public void Aggregate_WithNullCollection_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ResultError.Aggregate(null!));
        }

        [Fact]
        public void Aggregate_WithEmptyCollection_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => ResultError.Aggregate(Array.Empty<ResultError>()));
        }

        [Fact]
        public void Equals_WithSameMessageAndCode_ReturnsTrue()
        {
            // Arrange
            var error1 = new ResultError("Test error", "ERR001");
            var error2 = new ResultError("Test error", "ERR001");

            // Act & Assert
            Assert.Equal(error1, error2);
            Assert.True(error1 == error2);
            Assert.False(error1 != error2);
        }

        [Fact]
        public void Equals_WithDifferentMessage_ReturnsFalse()
        {
            // Arrange
            var error1 = new ResultError("Error 1");
            var error2 = new ResultError("Error 2");

            // Act & Assert
            Assert.NotEqual(error1, error2);
            Assert.False(error1 == error2);
            Assert.True(error1 != error2);
        }

        [Fact]
        public void Equals_WithDifferentCode_ReturnsFalse()
        {
            // Arrange
            var error1 = new ResultError("Test error", "ERR001");
            var error2 = new ResultError("Test error", "ERR002");

            // Act & Assert
            Assert.NotEqual(error1, error2);
        }

        [Fact]
        public void Equals_WithNull_ReturnsFalse()
        {
            // Arrange
            var error = new ResultError("Test error");

            // Act & Assert
            Assert.False(error.Equals(null));
        }

        [Fact]
        public void GetHashCode_SameErrors_ReturnsSameHashCode()
        {
            // Arrange
            var error1 = new ResultError("Test error", "ERR001");
            var error2 = new ResultError("Test error", "ERR001");

            // Act & Assert
            Assert.Equal(error1.GetHashCode(), error2.GetHashCode());
        }

        [Fact]
        public void ToString_WithoutCode_ReturnsMessage()
        {
            // Arrange
            var error = new ResultError("Test error");

            // Act
            var result = error.ToString();

            // Assert
            Assert.Equal("Test error", result);
        }

        [Fact]
        public void ToString_WithCode_ReturnsFormattedString()
        {
            // Arrange
            var error = new ResultError("Test error", "ERR001");

            // Act
            var result = error.ToString();

            // Assert
            Assert.Equal("[ERR001] Test error", result);
        }
    }
}
