using Cortex.States;
using Cortex.Streams.Operators;
using Cortex.Streams.Operators.Windows;
using Moq;

namespace Cortex.Streams.Tests
{
    public class SlidingWindowOperatorTests
    {
        [Fact]
        public void SlidingWindowOperator_GroupsItemsIntoOverlappingWindows()
        {
            // Arrange
            var windowSize = TimeSpan.FromSeconds(4);
            var slideInterval = TimeSpan.FromSeconds(2);
            var stateStore = new InMemoryStateStore<string, List<InputData>>("SlidingWindowStateStore");
            var emittedResults = new List<WindowResult<string, InputData>>();

            var windowOperator = new SlidingWindowOperator<InputData, string>(
                keySelector: x => x.Key,
                timestampSelector: x => x.EventTime,
                windowSize: windowSize,
                slideInterval: slideInterval,
                stateStore: stateStore);

            var sinkOperator = new SinkOperator<WindowResult<string, InputData>>(result =>
            {
                lock (emittedResults)
                {
                    emittedResults.Add(result);
                }
            });
            windowOperator.SetNext(sinkOperator);

            var now = DateTime.UtcNow;

            // Act - emit items
            windowOperator.Process(new InputData { Key = "A", Value = 1, EventTime = now });
            windowOperator.Process(new InputData { Key = "A", Value = 2, EventTime = now.AddSeconds(1) });

            // Wait for windows to close
            Thread.Sleep(5000);

            // Assert - with overlapping windows, items may appear in multiple windows
            Assert.True(emittedResults.Count >= 1);

            // Cleanup
            windowOperator.Dispose();
        }

        [Fact]
        public void SlidingWindowOperator_SeparatesItemsByKey()
        {
            // Arrange
            var windowSize = TimeSpan.FromSeconds(2);
            var slideInterval = TimeSpan.FromSeconds(1);
            var stateStore = new InMemoryStateStore<string, List<InputData>>("SlidingWindowStateStore");
            var emittedResults = new List<WindowResult<string, InputData>>();

            var windowOperator = new SlidingWindowOperator<InputData, string>(
                keySelector: x => x.Key,
                timestampSelector: x => x.EventTime,
                windowSize: windowSize,
                slideInterval: slideInterval,
                stateStore: stateStore);

            var sinkOperator = new SinkOperator<WindowResult<string, InputData>>(result =>
            {
                lock (emittedResults)
                {
                    emittedResults.Add(result);
                }
            });
            windowOperator.SetNext(sinkOperator);

            var now = DateTime.UtcNow;

            // Act - emit items for different keys
            windowOperator.Process(new InputData { Key = "A", Value = 1, EventTime = now });
            windowOperator.Process(new InputData { Key = "B", Value = 10, EventTime = now });

            // Wait for windows to close
            Thread.Sleep(3000);

            // Assert
            var keyAResults = emittedResults.Where(r => r.Key == "A").ToList();
            var keyBResults = emittedResults.Where(r => r.Key == "B").ToList();

            Assert.True(keyAResults.Count >= 1);
            Assert.True(keyBResults.Count >= 1);

            // Cleanup
            windowOperator.Dispose();
        }

        [Fact]
        public void SlidingWindowOperator_ThrowsOnSlideIntervalGreaterThanWindowSize()
        {
            // Arrange & Act & Assert
            var stateStore = new InMemoryStateStore<string, List<InputData>>("SlidingWindowStateStore");

            Assert.Throws<ArgumentException>(() =>
                new SlidingWindowOperator<InputData, string>(
                    keySelector: x => x.Key,
                    timestampSelector: x => x.EventTime,
                    windowSize: TimeSpan.FromSeconds(1),
                    slideInterval: TimeSpan.FromSeconds(2),
                    stateStore: stateStore));
        }

        [Fact]
        public void SlidingWindowOperator_ThrowsOnNullKeySelector()
        {
            // Arrange & Act & Assert
            var stateStore = new InMemoryStateStore<string, List<InputData>>("SlidingWindowStateStore");

            Assert.Throws<ArgumentNullException>(() =>
                new SlidingWindowOperator<InputData, string>(
                    keySelector: null,
                    timestampSelector: x => x.EventTime,
                    windowSize: TimeSpan.FromSeconds(2),
                    slideInterval: TimeSpan.FromSeconds(1),
                    stateStore: stateStore));
        }

        [Fact]
        public void SlidingWindowOperator_ThrowsOnNullTimestampSelector()
        {
            // Arrange & Act & Assert
            var stateStore = new InMemoryStateStore<string, List<InputData>>("SlidingWindowStateStore");

            Assert.Throws<ArgumentNullException>(() =>
                new SlidingWindowOperator<InputData, string>(
                    keySelector: x => x.Key,
                    timestampSelector: null,
                    windowSize: TimeSpan.FromSeconds(2),
                    slideInterval: TimeSpan.FromSeconds(1),
                    stateStore: stateStore));
        }

        [Fact]
        public void SlidingWindowOperator_ThrowsOnNullStateStore()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SlidingWindowOperator<InputData, string>(
                    keySelector: x => x.Key,
                    timestampSelector: x => x.EventTime,
                    windowSize: TimeSpan.FromSeconds(2),
                    slideInterval: TimeSpan.FromSeconds(1),
                    stateStore: null));
        }

        [Fact]
        public void SlidingWindowOperator_ThreadSafety_NoExceptionsThrown()
        {
            // Arrange
            var windowSize = TimeSpan.FromSeconds(2);
            var slideInterval = TimeSpan.FromSeconds(1);
            var stateStore = new InMemoryStateStore<string, List<InputData>>("SlidingWindowStateStore");
            var emittedResults = new List<WindowResult<string, InputData>>();
            var lockObj = new object();

            var windowOperator = new SlidingWindowOperator<InputData, string>(
                keySelector: x => x.Key,
                timestampSelector: x => x.EventTime,
                windowSize: windowSize,
                slideInterval: slideInterval,
                stateStore: stateStore);

            var sinkOperator = new SinkOperator<WindowResult<string, InputData>>(result =>
            {
                lock (lockObj)
                {
                    emittedResults.Add(result);
                }
            });
            windowOperator.SetNext(sinkOperator);

            var now = DateTime.UtcNow;

            // Act - emit items from multiple threads
            var tasks = new List<Task>();
            for (int i = 0; i < 50; i++)
            {
                int value = i;
                tasks.Add(Task.Run(() =>
                {
                    windowOperator.Process(new InputData { Key = "A", Value = value, EventTime = now });
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Wait for windows to close
            Thread.Sleep(3000);

            // Assert - no exceptions thrown and results are emitted
            Assert.True(emittedResults.Count >= 1);

            // Cleanup
            windowOperator.Dispose();
        }

        [Fact]
        public void SlidingWindowOperator_IntegrationWithStreamBuilder()
        {
            // Arrange
            var windowSize = TimeSpan.FromSeconds(2);
            var slideInterval = TimeSpan.FromSeconds(1);
            var emittedResults = new List<WindowResult<string, InputData>>();

            var stream = StreamBuilder<InputData>
                .CreateNewStream("Test Sliding Window Stream")
                .Stream()
                .SlidingWindow<string>(
                    keySelector: x => x.Key,
                    timestampSelector: x => x.EventTime,
                    windowSize: windowSize,
                    slideInterval: slideInterval)
                .Sink(result =>
                {
                    lock (emittedResults)
                    {
                        emittedResults.Add(result);
                    }
                })
                .Build();

            stream.Start();

            var now = DateTime.UtcNow;

            // Act
            stream.Emit(new InputData { Key = "A", Value = 1, EventTime = now });
            stream.Emit(new InputData { Key = "A", Value = 2, EventTime = now.AddMilliseconds(500) });

            // Wait for windows to close
            Thread.Sleep(3000);

            // Assert
            Assert.True(emittedResults.Count >= 1);

            stream.Stop();
        }

        [Fact]
        public void SlidingWindowOperator_WindowContainsItemsWithinWindowBoundaries()
        {
            // Arrange
            var windowSize = TimeSpan.FromSeconds(2);
            var slideInterval = TimeSpan.FromSeconds(1);
            var stateStore = new InMemoryStateStore<string, List<InputData>>("SlidingWindowStateStore");
            var emittedResults = new List<WindowResult<string, InputData>>();

            var windowOperator = new SlidingWindowOperator<InputData, string>(
                keySelector: x => x.Key,
                timestampSelector: x => x.EventTime,
                windowSize: windowSize,
                slideInterval: slideInterval,
                stateStore: stateStore);

            var sinkOperator = new SinkOperator<WindowResult<string, InputData>>(result =>
            {
                lock (emittedResults)
                {
                    emittedResults.Add(result);
                }
            });
            windowOperator.SetNext(sinkOperator);

            var now = DateTime.UtcNow;

            // Act
            windowOperator.Process(new InputData { Key = "A", Value = 1, EventTime = now });

            // Wait for windows to close
            Thread.Sleep(3000);

            // Assert - verify window boundaries
            foreach (var result in emittedResults)
            {
                Assert.True(result.WindowEnd > result.WindowStart);
                Assert.Equal(windowSize, result.WindowEnd - result.WindowStart);
            }

            // Cleanup
            windowOperator.Dispose();
        }

        public class InputData
        {
            public string Key { get; set; }
            public int Value { get; set; }
            public DateTime EventTime { get; set; }
        }
    }
}
