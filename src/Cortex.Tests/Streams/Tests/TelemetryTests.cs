using Cortex.Streams;
using Cortex.Streams.Operators;
using Cortex.Telemetry;
using Moq;

namespace Cortex.Tests.Streams.Tests
{
    public class TelemetryTests
    {
        #region Mock Telemetry Provider

        private static (Mock<ITelemetryProvider> provider, MockTelemetryState state) CreateMockTelemetryProvider()
        {
            var state = new MockTelemetryState();
            var mockProvider = new Mock<ITelemetryProvider>();
            var mockMetricsProvider = new Mock<IMetricsProvider>();
            var mockTracingProvider = new Mock<ITracingProvider>();

            // Setup counters
            mockMetricsProvider.Setup(m => m.CreateCounter(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string name, string desc) =>
                {
                    var counter = new MockCounter(name, state);
                    return counter;
                });

            // Setup histograms
            mockMetricsProvider.Setup(m => m.CreateHistogram(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string name, string desc) =>
                {
                    var histogram = new MockHistogram(name, state);
                    return histogram;
                });

            // Setup tracer
            mockTracingProvider.Setup(t => t.GetTracer(It.IsAny<string>()))
                .Returns((string name) =>
                {
                    var tracer = new MockTracer(name, state);
                    return tracer;
                });

            mockProvider.Setup(p => p.GetMetricsProvider()).Returns(mockMetricsProvider.Object);
            mockProvider.Setup(p => p.GetTracingProvider()).Returns(mockTracingProvider.Object);

            return (mockProvider, state);
        }

        private class MockTelemetryState
        {
            private readonly object _lock = new object();
            public Dictionary<string, double> CounterValues { get; } = new();
            public Dictionary<string, List<double>> HistogramValues { get; } = new();
            public List<string> SpanNames { get; } = new();
            public Dictionary<string, Dictionary<string, string>> SpanAttributes { get; } = new();
            public List<string> TracerNames { get; } = new();

            public void IncrementCounter(string name, double value)
            {
                lock (_lock)
                {
                    if (!CounterValues.ContainsKey(name))
                        CounterValues[name] = 0;
                    CounterValues[name] += value;
                }
            }

            public double GetCounterValue(string name)
            {
                lock (_lock)
                {
                    return CounterValues.TryGetValue(name, out var value) ? value : 0;
                }
            }

            public void RecordHistogram(string name, double value)
            {
                lock (_lock)
                {
                    if (!HistogramValues.ContainsKey(name))
                        HistogramValues[name] = new List<double>();
                    HistogramValues[name].Add(value);
                }
            }

            public void AddSpanName(string name)
            {
                lock (_lock)
                {
                    SpanNames.Add(name);
                }
            }

            public void AddTracerName(string name)
            {
                lock (_lock)
                {
                    TracerNames.Add(name);
                }
            }

            public void SetSpanAttribute(string spanName, string key, string value)
            {
                lock (_lock)
                {
                    if (!SpanAttributes.ContainsKey(spanName))
                        SpanAttributes[spanName] = new Dictionary<string, string>();
                    SpanAttributes[spanName][key] = value;
                }
            }
        }

        private class MockCounter : ICounter
        {
            private readonly string _name;
            private readonly MockTelemetryState _state;

            public MockCounter(string name, MockTelemetryState state)
            {
                _name = name;
                _state = state;
                _state.IncrementCounter(name, 0); // Initialize
            }

            public void Increment(double value = 1)
            {
                _state.IncrementCounter(_name, value);
            }
        }

        private class MockHistogram : IHistogram
        {
            private readonly string _name;
            private readonly MockTelemetryState _state;

            public MockHistogram(string name, MockTelemetryState state)
            {
                _name = name;
                _state = state;
            }

            public void Record(double value)
            {
                _state.RecordHistogram(_name, value);
            }
        }

        private class MockTracer : ITracer
        {
            private readonly string _name;
            private readonly MockTelemetryState _state;

            public MockTracer(string name, MockTelemetryState state)
            {
                _name = name;
                _state = state;
                _state.AddTracerName(name);
            }

            public ISpan StartSpan(string name)
            {
                return new MockSpan(name, _state);
            }
        }

        private class MockSpan : ISpan
        {
            private readonly string _name;
            private readonly MockTelemetryState _state;

            public MockSpan(string name, MockTelemetryState state)
            {
                _name = name;
                _state = state;
                _state.AddSpanName(name);
            }

            public void SetAttribute(string key, string value)
            {
                _state.SetSpanAttribute(_name, key, value);
            }

            public void AddEvent(string name, IDictionary<string, object>? attributes = null)
            {
                // Not tracked in this mock
            }

            public void Dispose()
            {
                // No-op
            }
        }

        #endregion

        #region Telemetry Optional Tests

        [Fact]
        public void Stream_WorksWithoutTelemetry()
        {
            // Arrange
            var receivedData = new List<int>();
            var stream = StreamBuilder<int, int>
                .CreateNewStream("TestStreamWithoutTelemetry")
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

            // Assert - Stream should work normally without telemetry
            Assert.Equal(new[] { 6, 8 }, receivedData);
        }

        [Fact]
        public void Stream_WorksWithNullTelemetryProvider()
        {
            // Arrange
            var receivedData = new List<int>();
            var stream = StreamBuilder<int, int>
                .CreateNewStream("TestStreamNullTelemetry")
                .WithTelemetry(null!)
                .Stream()
                .Map(x => x * 2)
                .Sink(x => receivedData.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(5);

            // Assert
            Assert.Single(receivedData);
            Assert.Equal(10, receivedData[0]);
        }

        #endregion

        #region MapOperator Telemetry Tests

        [Fact]
        public void MapOperator_WithTelemetry_RecordsMetrics()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();
            int result = 0;

            var stream = StreamBuilder<int, int>
                .CreateNewStream("MapTelemetryTest")
                .WithTelemetry(mockProvider.Object)
                .Stream()
                .Map(x => x * 2)
                .Sink(x => result = x)
                .Build();

            stream.Start();

            // Act
            stream.Emit(5);

            // Assert
            Assert.Equal(10, result);

            // Verify counter was incremented
            var processedCounter = state.CounterValues.FirstOrDefault(c => c.Key.Contains("map_operator_processed"));
            Assert.True(processedCounter.Value > 0, "MapOperator processed counter should be incremented");

            // Verify histogram was recorded
            var histogram = state.HistogramValues.FirstOrDefault(h => h.Key.Contains("map_operator_processing_time"));
            Assert.NotNull(histogram.Value);
            Assert.NotEmpty(histogram.Value);

            // Verify span was created
            Assert.Contains(state.SpanNames, s => s.Contains("MapOperator.Process"));
        }

        [Fact]
        public void MapOperator_WithTelemetry_RecordsErrorOnException()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();

            var mapOperator = new MapOperator<int, int>(x => throw new InvalidOperationException("Test exception"));
            mapOperator.SetTelemetryProvider(mockProvider.Object);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => mapOperator.Process(5));

            // Verify error span attribute was set
            var spanWithError = state.SpanAttributes.FirstOrDefault(s => s.Key.Contains("MapOperator.Process"));
            Assert.Equal("error", spanWithError.Value["status"]);
        }

        #endregion

        #region FilterOperator Telemetry Tests

        [Fact]
        public void FilterOperator_WithTelemetry_RecordsMetrics()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();
            var receivedData = new List<int>();

            var stream = StreamBuilder<int, int>
                .CreateNewStream("FilterTelemetryTest")
                .WithTelemetry(mockProvider.Object)
                .Stream()
                .Filter(x => x > 5)
                .Sink(x => receivedData.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(3);  // Should be filtered out
            stream.Emit(7);  // Should pass

            // Assert
            Assert.Single(receivedData);
            Assert.Equal(7, receivedData[0]);

            // Verify counters
            var processedCounter = state.CounterValues.FirstOrDefault(c => c.Key.Contains("filter_operator_processed"));
            Assert.Equal(2, processedCounter.Value);
        }

        [Fact]
        public void FilterOperator_WithTelemetry_RecordsFilteredOutCounter()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();
            var receivedData = new List<int>();

            var filterOperator = new FilterOperator<int>(x => x > 5);
            var sinkOperator = new SinkOperator<int>(x => receivedData.Add(x));
            filterOperator.SetNext(sinkOperator);
            filterOperator.SetTelemetryProvider(mockProvider.Object);

            // Act
            filterOperator.Process(3);  // Filtered out
            filterOperator.Process(7);  // Passes

            // Assert
            Assert.Single(receivedData);

            var filteredOutCounter = state.CounterValues.FirstOrDefault(c => c.Key.Contains("filter_operator_filtered_out"));
            Assert.Equal(1, filteredOutCounter.Value);
        }

        #endregion

        #region SinkOperator Telemetry Tests

        [Fact]
        public void SinkOperator_WithTelemetry_RecordsMetrics()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();
            int result = 0;

            var sinkOperator = new SinkOperator<int>(x => result = x);
            sinkOperator.SetTelemetryProvider(mockProvider.Object);

            // Act
            sinkOperator.Process(42);

            // Assert
            Assert.Equal(42, result);

            var processedCounter = state.CounterValues.FirstOrDefault(c => c.Key.Contains("sink_operator_processed"));
            Assert.Equal(1, processedCounter.Value);

            Assert.Contains(state.SpanNames, s => s.Contains("SinkOperator.Process"));
        }

        [Fact]
        public void SinkOperator_WithTelemetry_RecordsErrorOnException()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();

            var sinkOperator = new SinkOperator<int>(x => throw new InvalidOperationException("Test exception"));
            sinkOperator.SetTelemetryProvider(mockProvider.Object);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => sinkOperator.Process(5));

            var spanWithError = state.SpanAttributes.FirstOrDefault(s => s.Key.Contains("SinkOperator.Process"));
            Assert.Equal("error", spanWithError.Value["status"]);
        }

        #endregion

        #region FlatMapOperator Telemetry Tests

        [Fact]
        public void FlatMapOperator_WithTelemetry_RecordsMetrics()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();
            var receivedData = new List<int>();

            var stream = StreamBuilder<string, string>
                .CreateNewStream("FlatMapTelemetryTest")
                .WithTelemetry(mockProvider.Object)
                .Stream()
                .FlatMap<int>(s => s.Split(',').Select(int.Parse))
                .Sink(x => receivedData.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit("1,2,3");

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, receivedData);

            var processedCounter = state.CounterValues.FirstOrDefault(c => c.Key.Contains("flatmap_operator_processed"));
            Assert.Equal(1, processedCounter.Value);

            var emittedCounter = state.CounterValues.FirstOrDefault(c => c.Key.Contains("flatmap_operator_emitted"));
            Assert.Equal(3, emittedCounter.Value);
        }

        #endregion

        #region GroupByKeyOperator Telemetry Tests

        [Fact]
        public void GroupByKeyOperator_WithTelemetry_RecordsMetrics()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();
            var receivedGroups = new List<KeyValuePair<string, List<int>>>();

            var stream = StreamBuilder<int, int>
                .CreateNewStream("GroupByTelemetryTest")
                .WithTelemetry(mockProvider.Object)
                .Stream()
                .GroupBy(x => x % 2 == 0 ? "even" : "odd")
                .Sink(x => receivedGroups.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);
            stream.Emit(2);
            stream.Emit(3);

            // Assert
            Assert.Equal(3, receivedGroups.Count);

            var processedCounter = state.CounterValues.FirstOrDefault(c => c.Key.Contains("groupby_operator_processed"));
            Assert.Equal(3, processedCounter.Value);

            Assert.Contains(state.SpanNames, s => s.Contains("GroupByKeyOperator.Process"));
        }

        #endregion

        #region AggregateOperator Telemetry Tests

        [Fact]
        public void AggregateOperator_WithTelemetry_RecordsMetrics()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();
            var receivedAggregates = new List<KeyValuePair<string, int>>();

            var stream = StreamBuilder<int, int>
                .CreateNewStream("AggregateTelemetryTest")
                .WithTelemetry(mockProvider.Object)
                .Stream()
                .Aggregate<string, int>(
                    x => "sum",
                    (acc, x) => acc + x)
                .Sink(x => receivedAggregates.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);
            stream.Emit(2);
            stream.Emit(3);

            // Assert
            Assert.Equal(3, receivedAggregates.Count);
            Assert.Equal(6, receivedAggregates[2].Value);

            var processedCounter = state.CounterValues.FirstOrDefault(c => c.Key.Contains("aggregate_operator_processed"));
            Assert.Equal(3, processedCounter.Value);
        }

        #endregion

        #region Branch and Fork Operator Telemetry Tests

        [Fact]
        public void BranchOperator_WithTelemetry_RecordsMetrics()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();
            var branch1Data = new List<int>();
            var branch2Data = new List<int>();

            var stream = StreamBuilder<int, int>
                .CreateNewStream("BranchTelemetryTest")
                .WithTelemetry(mockProvider.Object)
                .Stream()
                .AddBranch("positive", b => b
                    .Filter(x => x > 0)
                    .Sink(x => branch1Data.Add(x)))
                .AddBranch("negative", b => b
                    .Filter(x => x < 0)
                    .Sink(x => branch2Data.Add(x)))
                .Build();

            stream.Start();

            // Act
            stream.Emit(5);
            stream.Emit(-3);

            // Assert
            Assert.Single(branch1Data);
            Assert.Equal(5, branch1Data[0]);
            Assert.Single(branch2Data);
            Assert.Equal(-3, branch2Data[0]);

            // Verify fork operator metrics
            var forkCounter = state.CounterValues.FirstOrDefault(c => c.Key.Contains("fork_operator_processed"));
            Assert.Equal(2, forkCounter.Value);

            // Verify branch operator metrics
            var branchCounters = state.CounterValues.Where(c => c.Key.Contains("branch_operator_processed")).ToList();
            Assert.True(branchCounters.Count >= 1, "Branch operator counters should exist");
        }

        #endregion

        #region End-to-End Telemetry Propagation Tests

        [Fact]
        public void Telemetry_PropagatesThroughEntirePipeline()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();
            var receivedData = new List<string>();

            var stream = StreamBuilder<int, int>
                .CreateNewStream("E2ETelemetryTest")
                .WithTelemetry(mockProvider.Object)
                .Stream()
                .Map(x => x * 2)
                .Filter(x => x > 5)
                .Map(x => x.ToString())
                .Sink(x => receivedData.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);  // 2, filtered out
            stream.Emit(3);  // 6, passes
            stream.Emit(4);  // 8, passes

            // Assert
            Assert.Equal(new[] { "6", "8" }, receivedData);

            // Verify telemetry was recorded for each operator type
            Assert.Contains(state.CounterValues.Keys, k => k.Contains("map_operator_processed"));
            Assert.Contains(state.CounterValues.Keys, k => k.Contains("filter_operator_processed"));
            Assert.Contains(state.CounterValues.Keys, k => k.Contains("sink_operator_processed"));
        }

        [Fact]
        public void Telemetry_CorrectlyTracksProcessingTime()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();

            var mapOperator = new MapOperator<int, int>(x =>
            {
                Thread.Sleep(10); // Simulate work
                return x * 2;
            });
            var sinkOperator = new SinkOperator<int>(x => { });
            mapOperator.SetNext(sinkOperator);
            mapOperator.SetTelemetryProvider(mockProvider.Object);

            // Act
            mapOperator.Process(5);

            // Assert
            var histogram = state.HistogramValues.FirstOrDefault(h => h.Key.Contains("map_operator_processing_time"));
            Assert.NotEmpty(histogram.Value);
            Assert.True(histogram.Value[0] >= 10, "Processing time should be at least 10ms");
        }

        #endregion

        #region SinkOperatorAdapter Telemetry Tests

        [Fact]
        public void SinkOperatorAdapter_WithTelemetry_RecordsMetrics()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();
            int result = 0;

            var mockSinkOperator = new Mock<ISinkOperator<int>>();
            mockSinkOperator.Setup(s => s.Process(It.IsAny<int>())).Callback<int>(x => result = x);

            var sinkAdapter = new SinkOperatorAdapter<int>(mockSinkOperator.Object);
            sinkAdapter.SetTelemetryProvider(mockProvider.Object);

            // Act
            sinkAdapter.Process(42);

            // Assert
            Assert.Equal(42, result);

            var processedCounter = state.CounterValues.FirstOrDefault(c => c.Key.Contains("sink_operator_adapter_processed"));
            Assert.Equal(1, processedCounter.Value);

            Assert.Contains(state.SpanNames, s => s.Contains("SinkOperatorAdapter.Process"));
        }

        [Fact]
        public void SinkOperatorAdapter_WithTelemetry_RecordsErrorOnException()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();

            var mockSinkOperator = new Mock<ISinkOperator<int>>();
            mockSinkOperator.Setup(s => s.Process(It.IsAny<int>())).Throws(new InvalidOperationException("Test exception"));

            var sinkAdapter = new SinkOperatorAdapter<int>(mockSinkOperator.Object);
            sinkAdapter.SetTelemetryProvider(mockProvider.Object);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => sinkAdapter.Process(5));

            var spanWithError = state.SpanAttributes.FirstOrDefault(s => s.Key.Contains("SinkOperatorAdapter.Process"));
            Assert.Equal("error", spanWithError.Value["status"]);
        }

        #endregion

        #region Telemetry Reset Tests

        [Fact]
        public void Operator_CanChangeOrRemoveTelemetryProvider()
        {
            // Arrange
            var (mockProvider1, state1) = CreateMockTelemetryProvider();
            var (mockProvider2, state2) = CreateMockTelemetryProvider();
            int result = 0;

            var mapOperator = new MapOperator<int, int>(x => x * 2);
            var sinkOperator = new SinkOperator<int>(x => result = x);
            mapOperator.SetNext(sinkOperator);

            // Act - Process with first provider
            mapOperator.SetTelemetryProvider(mockProvider1.Object);
            mapOperator.Process(5);
            Assert.Equal(10, result);
            Assert.Equal(1, state1.CounterValues.Values.FirstOrDefault(v => v > 0));

            // Act - Switch to second provider
            mapOperator.SetTelemetryProvider(mockProvider2.Object);
            mapOperator.Process(10);
            Assert.Equal(20, result);
            Assert.Equal(1, state2.CounterValues.Values.FirstOrDefault(v => v > 0));

            // First provider should not have received more increments
            Assert.Equal(1, state1.CounterValues.Values.FirstOrDefault(v => v > 0));

            // Act - Remove telemetry (set to null)
            mapOperator.SetTelemetryProvider(null!);
            mapOperator.Process(15);
            Assert.Equal(30, result);
            // Both states should remain unchanged
            Assert.Equal(1, state1.CounterValues.Values.FirstOrDefault(v => v > 0));
            Assert.Equal(1, state2.CounterValues.Values.FirstOrDefault(v => v > 0));
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public void Telemetry_IsThreadSafe()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();
            var receivedData = new System.Collections.Concurrent.ConcurrentBag<int>();

            var stream = StreamBuilder<int, int>
                .CreateNewStream("ThreadSafeTelemetryTest")
                .WithTelemetry(mockProvider.Object)
                .Stream()
                .Map(x => x * 2)
                .Sink(x => receivedData.Add(x))
                .Build();

            stream.Start();

            // Act - Emit from multiple threads
            var tasks = Enumerable.Range(0, 100)
                .Select(i => Task.Run(() => stream.Emit(i)))
                .ToArray();

            Task.WaitAll(tasks);

            // Assert
            Assert.Equal(100, receivedData.Count);

            var processedCounter = state.CounterValues.FirstOrDefault(c => c.Key.Contains("map_operator_processed"));
            Assert.Equal(100, processedCounter.Value);
        }

        #endregion
    }
}
