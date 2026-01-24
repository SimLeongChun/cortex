using Cortex.Streams.Operators.Windows;

namespace Cortex.Streams.Tests
{
    public class WindowResultTests
    {
        [Fact]
        public void WindowResult_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange
            var key = "TestKey";
            var windowStart = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var windowEnd = new DateTime(2024, 1, 1, 10, 5, 0, DateTimeKind.Utc);
            var items = new List<int> { 1, 2, 3 };

            // Act
            var result = new WindowResult<string, int>(key, windowStart, windowEnd, items);

            // Assert
            Assert.Equal(key, result.Key);
            Assert.Equal(windowStart, result.WindowStart);
            Assert.Equal(windowEnd, result.WindowEnd);
            Assert.Equal(3, result.Items.Count);
            Assert.Equal(items, result.Items);
        }

        [Fact]
        public void WindowResult_Constructor_ThrowsOnNullItems()
        {
            // Arrange
            var key = "TestKey";
            var windowStart = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var windowEnd = new DateTime(2024, 1, 1, 10, 5, 0, DateTimeKind.Utc);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new WindowResult<string, int>(key, windowStart, windowEnd, null));
        }

        [Fact]
        public void WindowResult_ToString_ReturnsFormattedString()
        {
            // Arrange
            var key = "TestKey";
            var windowStart = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var windowEnd = new DateTime(2024, 1, 1, 10, 5, 0, DateTimeKind.Utc);
            var items = new List<int> { 1, 2, 3 };
            var result = new WindowResult<string, int>(key, windowStart, windowEnd, items);

            // Act
            var toString = result.ToString();

            // Assert
            Assert.Contains("TestKey", toString);
            Assert.Contains("Count=3", toString);
        }

        [Fact]
        public void WindowResult_WithEmptyItems_ReturnsZeroCount()
        {
            // Arrange
            var key = "TestKey";
            var windowStart = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var windowEnd = new DateTime(2024, 1, 1, 10, 5, 0, DateTimeKind.Utc);
            var items = new List<int>();

            // Act
            var result = new WindowResult<string, int>(key, windowStart, windowEnd, items);

            // Assert
            Assert.Empty(result.Items);
            Assert.Equal(0, result.Items.Count);
        }

        [Fact]
        public void WindowResult_Items_IsReadOnly()
        {
            // Arrange
            var key = "TestKey";
            var windowStart = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var windowEnd = new DateTime(2024, 1, 1, 10, 5, 0, DateTimeKind.Utc);
            var items = new List<int> { 1, 2, 3 };
            var result = new WindowResult<string, int>(key, windowStart, windowEnd, items);

            // Act & Assert - Items property is IReadOnlyList, so direct modification is not possible
            Assert.IsAssignableFrom<IReadOnlyList<int>>(result.Items);
        }

        [Fact]
        public void WindowResult_WithNullKey_AllowsNullKey()
        {
            // Arrange
            var windowStart = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var windowEnd = new DateTime(2024, 1, 1, 10, 5, 0, DateTimeKind.Utc);
            var items = new List<int> { 1, 2, 3 };

            // Act
            var result = new WindowResult<string, int>(null, windowStart, windowEnd, items);

            // Assert
            Assert.Null(result.Key);
        }

        [Fact]
        public void WindowResult_WithComplexType_WorksCorrectly()
        {
            // Arrange
            var key = "TestKey";
            var windowStart = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var windowEnd = new DateTime(2024, 1, 1, 10, 5, 0, DateTimeKind.Utc);
            var items = new List<TestData>
            {
                new TestData { Id = 1, Name = "Item1" },
                new TestData { Id = 2, Name = "Item2" }
            };

            // Act
            var result = new WindowResult<string, TestData>(key, windowStart, windowEnd, items);

            // Assert
            Assert.Equal(2, result.Items.Count);
            Assert.Equal("Item1", result.Items[0].Name);
            Assert.Equal("Item2", result.Items[1].Name);
        }

        [Fact]
        public void WindowResult_WindowDuration_CanBeCalculated()
        {
            // Arrange
            var key = "TestKey";
            var windowStart = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var windowEnd = new DateTime(2024, 1, 1, 10, 5, 0, DateTimeKind.Utc);
            var items = new List<int> { 1, 2, 3 };
            var result = new WindowResult<string, int>(key, windowStart, windowEnd, items);

            // Act
            var duration = result.WindowEnd - result.WindowStart;

            // Assert
            Assert.Equal(TimeSpan.FromMinutes(5), duration);
        }

        public class TestData
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
