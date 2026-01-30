using Cortex.Streams.Operators;

namespace Cortex.Streams.Tests
{
    /// <summary>
    /// Unit tests for the FanOut feature verifying individual sink operations.
    /// </summary>
    public class FanOutOperatorTests
    {
        #region Basic FanOut Tests

        [Fact]
        public void FanOut_SingleSink_ReceivesAllData()
        {
            // Arrange
            var receivedData = new List<int>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestFanOutSingleSink")
                .Stream()
                .FanOut(fanOut => fanOut
                    .To("sink1", x => receivedData.Add(x)))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);
            stream.Emit(2);
            stream.Emit(3);

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, receivedData);
        }

        [Fact]
        public void FanOut_MultipleSinks_AllReceiveData()
        {
            // Arrange
            var sink1Data = new List<int>();
            var sink2Data = new List<int>();
            var sink3Data = new List<int>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestFanOutMultipleSinks")
                .Stream()
                .FanOut(fanOut => fanOut
                    .To("database", x => sink1Data.Add(x))
                    .To("kafka", x => sink2Data.Add(x))
                    .To("logging", x => sink3Data.Add(x)))
                .Build();

            stream.Start();

            // Act
            stream.Emit(10);
            stream.Emit(20);

            // Assert - All sinks should receive all data
            Assert.Equal(new[] { 10, 20 }, sink1Data);
            Assert.Equal(new[] { 10, 20 }, sink2Data);
            Assert.Equal(new[] { 10, 20 }, sink3Data);
        }

        [Fact]
        public void FanOut_WithFilter_OnlyMatchingDataReachesSink()
        {
            // Arrange
            var allData = new List<int>();
            var highValueData = new List<int>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestFanOutWithFilter")
                .Stream()
                .FanOut(fanOut => fanOut
                    .To("all", x => allData.Add(x))
                    .To("high-value", x => x > 50, x => highValueData.Add(x)))
                .Build();

            stream.Start();

            // Act
            stream.Emit(25);
            stream.Emit(75);
            stream.Emit(30);
            stream.Emit(100);

            // Assert
            Assert.Equal(new[] { 25, 75, 30, 100 }, allData);
            Assert.Equal(new[] { 75, 100 }, highValueData);
        }

        #endregion

        #region FanOut with Transformations

        [Fact]
        public void FanOut_AfterMap_ReceivesTransformedData()
        {
            // Arrange
            var receivedData = new List<int>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestFanOutAfterMap")
                .Stream()
                .Map(x => x * 2)
                .FanOut(fanOut => fanOut
                    .To("doubled", x => receivedData.Add(x)))
                .Build();

            stream.Start();

            // Act
            stream.Emit(5);
            stream.Emit(10);

            // Assert
            Assert.Equal(new[] { 10, 20 }, receivedData);
        }

        [Fact]
        public void FanOut_AfterFilter_ReceivesFilteredData()
        {
            // Arrange
            var receivedData = new List<int>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestFanOutAfterFilter")
                .Stream()
                .Filter(x => x % 2 == 0)
                .FanOut(fanOut => fanOut
                    .To("even-numbers", x => receivedData.Add(x)))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);
            stream.Emit(2);
            stream.Emit(3);
            stream.Emit(4);

            // Assert
            Assert.Equal(new[] { 2, 4 }, receivedData);
        }

        [Fact]
        public void FanOut_WithToWithTransform_TransformsDataForSpecificSink()
        {
            // Arrange
            var originalData = new List<int>();
            var transformedData = new List<string>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestFanOutWithTransform")
                .Stream()
                .FanOut(fanOut => fanOut
                    .To("original", x => originalData.Add(x))
                    .ToWithTransform("formatted", x => $"Value: {x}", s => transformedData.Add(s)))
                .Build();

            stream.Start();

            // Act
            stream.Emit(42);
            stream.Emit(100);

            // Assert
            Assert.Equal(new[] { 42, 100 }, originalData);
            Assert.Equal(new[] { "Value: 42", "Value: 100" }, transformedData);
        }

        #endregion

        #region FanOut with ISinkOperator

        [Fact]
        public void FanOut_WithSinkOperator_UsesCustomOperator()
        {
            // Arrange
            var customSink = new TestSinkOperator<int>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestFanOutWithSinkOperator")
                .Stream()
                .FanOut(fanOut => fanOut
                    .To("custom-sink", customSink))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);
            stream.Emit(2);

            // Assert
            Assert.Equal(new[] { 1, 2 }, customSink.ReceivedData);
        }

        [Fact]
        public void FanOut_WithFilteredSinkOperator_FiltersCorrectly()
        {
            // Arrange
            var customSink = new TestSinkOperator<int>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestFanOutFilteredSinkOperator")
                .Stream()
                .FanOut(fanOut => fanOut
                    .To("filtered-custom", x => x > 5, customSink))
                .Build();

            stream.Start();

            // Act
            stream.Emit(3);
            stream.Emit(7);
            stream.Emit(2);
            stream.Emit(10);

            // Assert
            Assert.Equal(new[] { 7, 10 }, customSink.ReceivedData);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void FanOut_NoSinks_ThrowsInvalidOperationException()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                StreamBuilder<int>
                    .CreateNewStream("TestFanOutNoSinks")
                    .Stream()
                    .FanOut(fanOut => { /* No sinks added */ })
                    .Build());

            Assert.Contains("at least one sink", exception.Message);
        }

        [Fact]
        public void FanOut_NullConfig_ThrowsArgumentNullException()
        {
            // Arrange
            var builder = StreamBuilder<int>
                .CreateNewStream("TestFanOutNullConfig")
                .Stream();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => builder.FanOut(null));
        }

        [Fact]
        public void FanOut_EmptySinkName_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() =>
                StreamBuilder<int>
                    .CreateNewStream("TestFanOutEmptyName")
                    .Stream()
                    .FanOut(fanOut => fanOut.To("", x => { })));
        }

        [Fact]
        public void FanOut_NullSinkFunction_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                StreamBuilder<int>
                    .CreateNewStream("TestFanOutNullSink")
                    .Stream()
                    .FanOut(fanOut => fanOut.To("sink", (Action<int>)null)));
        }

        [Fact]
        public void FanOut_DuplicateSinkName_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                StreamBuilder<int>
                    .CreateNewStream("TestFanOutDuplicateName")
                    .Stream()
                    .FanOut(fanOut => fanOut
                        .To("database", x => { })
                        .To("database", x => { })));

            Assert.Contains("database", exception.Message);
            Assert.Contains("already been added", exception.Message);
        }

        [Fact]
        public void FanOut_NullPredicate_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                StreamBuilder<int>
                    .CreateNewStream("TestFanOutNullPredicate")
                    .Stream()
                    .FanOut(fanOut => fanOut.To("sink", null, x => { })));
        }

        #endregion

        #region Complex Pipeline Tests

        [Fact]
        public void FanOut_ComplexPipeline_MapFilterFanOut()
        {
            // Arrange
            var dbData = new List<string>();
            var alertData = new List<string>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestComplexPipeline")
                .Stream()
                .Filter(x => x > 0)
                .Map(x => $"Order-{x}")
                .FanOut(fanOut => fanOut
                    .To("database", s => dbData.Add(s))
                    .To("alerts", s => s.Contains("100"), s => alertData.Add(s)))
                .Build();

            stream.Start();

            // Act
            stream.Emit(-5);   // Filtered out
            stream.Emit(50);   // -> "Order-50" -> database only
            stream.Emit(100);  // -> "Order-100" -> database + alerts

            // Assert
            Assert.Equal(new[] { "Order-50", "Order-100" }, dbData);
            Assert.Equal(new[] { "Order-100" }, alertData);
        }

        [Fact]
        public void FanOut_WithMultipleFilters_EachSinkReceivesCorrectData()
        {
            // Arrange
            var lowData = new List<int>();
            var mediumData = new List<int>();
            var highData = new List<int>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestMultipleFilters")
                .Stream()
                .FanOut(fanOut => fanOut
                    .To("low", x => x < 10, x => lowData.Add(x))
                    .To("medium", x => x >= 10 && x < 100, x => mediumData.Add(x))
                    .To("high", x => x >= 100, x => highData.Add(x)))
                .Build();

            stream.Start();

            // Act
            stream.Emit(5);
            stream.Emit(50);
            stream.Emit(500);
            stream.Emit(8);
            stream.Emit(75);

            // Assert
            Assert.Equal(new[] { 5, 8 }, lowData);
            Assert.Equal(new[] { 50, 75 }, mediumData);
            Assert.Equal(new[] { 500 }, highData);
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Test sink operator for verifying ISinkOperator integration.
        /// </summary>
        private class TestSinkOperator<T> : ISinkOperator<T>
        {
            public List<T> ReceivedData { get; } = new List<T>();
            public bool IsStarted { get; private set; }
            public bool IsStopped { get; private set; }

            public void Start() => IsStarted = true;
            public void Process(T input) => ReceivedData.Add(input);
            public void Stop() => IsStopped = true;
        }

        #endregion
    }
}
