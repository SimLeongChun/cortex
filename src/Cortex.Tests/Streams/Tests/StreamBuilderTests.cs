namespace Cortex.Streams.Tests
{
    public class StreamBuilderTests
    {
        [Fact]
        public void StreamBuilder_CreatesAndRunsStreamCorrectly()
        {
            // Arrange
            var receivedData = new List<int>();
            var stream = StreamBuilder<int>
                .CreateNewStream("TestStream")
                .Stream()
                .Map(x => x * 2)
                .Filter(x => x > 5)
                .Sink(x => receivedData.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);
            stream.Emit(2);
            stream.Emit(3);
            stream.Emit(4);

            // Assert
            Assert.Equal(new[] { 6, 8 }, receivedData);
        }

        [Fact]
        public void Build_ShouldCreateStreamSuccessfully()
        {
            // Arrange
            var builder = StreamBuilder<int>.CreateNewStream("TestStream")
                .Stream()
                .Map(x => x * 2)
                .Filter(x => x > 5);

            // Act
            var stream = builder.Build();

            // Assert
            Assert.NotNull(stream);
        }

        [Fact]
        public void StreamBuilder_WithBranchesAndSink_ShouldProcessBothBranchesAndMainSink()
        {
            // Arrange - Bug scenario: branches not triggered when Sink in main pipeline
            var branch1Data = new List<int>();
            var branch2Data = new List<int>();
            var mainSinkData = new List<int>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestStreamWithBranchesAndSink")
                .Stream()
                .Map(x => x * 2)
                .AddBranch("EvenBranch", branch => branch
                    .Filter(x => x % 4 == 0)
                    .Sink(x => branch1Data.Add(x)))
                .AddBranch("OddBranch", branch => branch
                    .Filter(x => x % 4 != 0)
                    .Sink(x => branch2Data.Add(x)))
                .Sink(x => mainSinkData.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);  // -> 2 (branch2, main)
            stream.Emit(2);  // -> 4 (branch1, main)
            stream.Emit(3);  // -> 6 (branch2, main)
            stream.Emit(4);  // -> 8 (branch1, main)

            // Assert - All branches and main sink should receive data
            Assert.Equal(new[] { 4, 8 }, branch1Data);      // Even multiples of 4
            Assert.Equal(new[] { 2, 6 }, branch2Data);      // Not multiples of 4
            Assert.Equal(new[] { 2, 4, 6, 8 }, mainSinkData); // All data flows to main sink
        }

        [Fact]
        public void StreamBuilder_WithBranchesAndMapAfterBranch_ShouldContinueProcessingAfterFork()
        {
            // Arrange - Test that Map works after branches
            var branchData = new List<int>();
            var mainSinkData = new List<string>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestStreamMapAfterBranch")
                .Stream()
                .AddBranch("NumberBranch", branch => branch
                    .Sink(x => branchData.Add(x)))
                .Map(x => $"Value: {x}")
                .Sink(x => mainSinkData.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);
            stream.Emit(2);

            // Assert
            Assert.Equal(new[] { 1, 2 }, branchData);
            Assert.Equal(new[] { "Value: 1", "Value: 2" }, mainSinkData);
        }

        [Fact]
        public void StreamBuilder_WithBranchesAndFilterAfterBranch_ShouldContinueProcessingAfterFork()
        {
            // Arrange - Test that Filter works after branches
            var branchData = new List<int>();
            var mainSinkData = new List<int>();

            var stream = StreamBuilder<int>
                .CreateNewStream("TestStreamFilterAfterBranch")
                .Stream()
                .AddBranch("AllBranch", branch => branch
                    .Sink(x => branchData.Add(x)))
                .Filter(x => x > 5)
                .Sink(x => mainSinkData.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(3);
            stream.Emit(7);
            stream.Emit(10);

            // Assert
            Assert.Equal(new[] { 3, 7, 10 }, branchData);  // Branch gets all data
            Assert.Equal(new[] { 7, 10 }, mainSinkData);   // Main sink only gets filtered data
        }
    }
}
