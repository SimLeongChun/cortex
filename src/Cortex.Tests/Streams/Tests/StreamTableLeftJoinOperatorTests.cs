using Cortex.States;
using Cortex.Streams;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Cortex.Telemetry;
using Moq;

namespace Cortex.Tests.Streams.Tests
{
    public class StreamTableLeftJoinOperatorTests
    {
        #region Basic Left Join Tests

        [Fact]
        public void LeftJoin_WithMatchingKey_ShouldEmitJoinedResult()
        {
            // Arrange
            var rightStore = new InMemoryStateStore<int, string>("RightStore");
            rightStore.Put(1, "Customer1");
            rightStore.Put(2, "Customer2");

            var results = new List<string>();
            var joinOperator = new StreamTableLeftJoinOperator<int, string, int, string>(
                left => left,
                (left, right) => $"Order:{left}-Customer:{right}",
                rightStore);

            var sinkOperator = new SinkOperator<string>(x => results.Add(x));
            joinOperator.SetNext(sinkOperator);

            // Act
            joinOperator.Process(1);
            joinOperator.Process(2);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal("Order:1-Customer:Customer1", results[0]);
            Assert.Equal("Order:2-Customer:Customer2", results[1]);
        }

        [Fact]
        public void LeftJoin_WithNoMatchingKey_ShouldEmitResultWithDefaultRight()
        {
            // Arrange
            var rightStore = new InMemoryStateStore<int, string>("RightStore");
            rightStore.Put(1, "Customer1");

            var results = new List<string>();
            var joinOperator = new StreamTableLeftJoinOperator<int, string, int, string>(
                left => left,
                (left, right) => $"Order:{left}-Customer:{right ?? "UNKNOWN"}",
                rightStore);

            var sinkOperator = new SinkOperator<string>(x => results.Add(x));
            joinOperator.SetNext(sinkOperator);

            // Act
            joinOperator.Process(1);  // Has match
            joinOperator.Process(99); // No match

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal("Order:1-Customer:Customer1", results[0]);
            Assert.Equal("Order:99-Customer:UNKNOWN", results[1]); // Left join emits with null/default
        }

        [Fact]
        public void LeftJoin_WithAllUnmatchedKeys_ShouldEmitAllResults()
        {
            // Arrange
            var rightStore = new InMemoryStateStore<int, string>("RightStore");
            // Empty store - no matches possible

            var results = new List<string>();
            var joinOperator = new StreamTableLeftJoinOperator<int, string, int, string>(
                left => left,
                (left, right) => $"Order:{left}-HasCustomer:{right != null}",
                rightStore);

            var sinkOperator = new SinkOperator<string>(x => results.Add(x));
            joinOperator.SetNext(sinkOperator);

            // Act
            joinOperator.Process(1);
            joinOperator.Process(2);
            joinOperator.Process(3);

            // Assert - all 3 should emit (unlike inner join which would emit 0)
            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.Contains("HasCustomer:False", r));
        }

        #endregion

        #region Comparison with Inner Join Behavior

        [Fact]
        public void LeftJoin_EmitsMoreResultsThanInnerJoin_WhenSomeKeysDoNotMatch()
        {
            // Arrange
            var rightStore = new InMemoryStateStore<int, string>("RightStore");
            rightStore.Put(1, "Customer1");
            rightStore.Put(3, "Customer3");

            var leftJoinResults = new List<int>();
            var innerJoinResults = new List<int>();

            var leftJoinOperator = new StreamTableLeftJoinOperator<int, string, int, int>(
                left => left,
                (left, right) => left,
                rightStore);

            var innerJoinOperator = new StreamTableJoinOperator<int, string, int, int>(
                left => left,
                (left, right) => left,
                rightStore);

            leftJoinOperator.SetNext(new SinkOperator<int>(x => leftJoinResults.Add(x)));
            innerJoinOperator.SetNext(new SinkOperator<int>(x => innerJoinResults.Add(x)));

            // Act - process orders 1, 2, 3 (only 1 and 3 have matching customers)
            leftJoinOperator.Process(1);
            leftJoinOperator.Process(2);
            leftJoinOperator.Process(3);

            innerJoinOperator.Process(1);
            innerJoinOperator.Process(2);
            innerJoinOperator.Process(3);

            // Assert
            Assert.Equal(3, leftJoinResults.Count);  // Left join: emits all
            Assert.Equal(2, innerJoinResults.Count); // Inner join: only matched keys
        }

        #endregion

        #region Complex Type Tests

        public record Order(int OrderId, int CustomerId, decimal Amount);
        public record Customer(int CustomerId, string Name, string Email);
        public record EnrichedOrder(int OrderId, decimal Amount, string? CustomerName);

        [Fact]
        public void LeftJoin_WithComplexTypes_ShouldHandleNullRight()
        {
            // Arrange
            var customerStore = new InMemoryStateStore<int, Customer>("CustomerStore");
            customerStore.Put(100, new Customer(100, "Alice", "alice@test.com"));
            customerStore.Put(200, new Customer(200, "Bob", "bob@test.com"));

            var results = new List<EnrichedOrder>();
            var joinOperator = new StreamTableLeftJoinOperator<Order, Customer, int, EnrichedOrder>(
                order => order.CustomerId,
                (order, customer) => new EnrichedOrder(order.OrderId, order.Amount, customer?.Name),
                customerStore);

            joinOperator.SetNext(new SinkOperator<EnrichedOrder>(x => results.Add(x)));

            // Act
            joinOperator.Process(new Order(1, 100, 50.00m));  // Alice
            joinOperator.Process(new Order(2, 999, 75.00m)); // Unknown customer
            joinOperator.Process(new Order(3, 200, 25.00m)); // Bob

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal("Alice", results[0].CustomerName);
            Assert.Null(results[1].CustomerName); // Left join handles missing customer
            Assert.Equal("Bob", results[2].CustomerName);
        }

        #endregion

        #region Null Handling Tests

        [Fact]
        public void LeftJoin_WithNullableValueType_ShouldHandleCorrectly()
        {
            // Arrange
            var rightStore = new InMemoryStateStore<string, int?>("RightStore");
            rightStore.Put("key1", 100);
            rightStore.Put("key2", null); // Explicitly stored null

            var results = new List<string>();
            var joinOperator = new StreamTableLeftJoinOperator<string, int?, string, string>(
                left => left,
                (left, right) => $"Key:{left}-Value:{right?.ToString() ?? "NULL"}",
                rightStore);

            joinOperator.SetNext(new SinkOperator<string>(x => results.Add(x)));

            // Act
            joinOperator.Process("key1");  // Has value
            joinOperator.Process("key2");  // Has null value
            joinOperator.Process("key3");  // Key doesn't exist

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal("Key:key1-Value:100", results[0]);
            Assert.Equal("Key:key2-Value:NULL", results[1]);  // Stored null
            Assert.Equal("Key:key3-Value:NULL", results[2]);  // Missing key
        }

        #endregion

        #region Constructor Validation Tests

        [Fact]
        public void Constructor_WithNullKeySelector_ShouldThrowArgumentNullException()
        {
            // Arrange
            var rightStore = new InMemoryStateStore<int, string>("RightStore");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new StreamTableLeftJoinOperator<int, string, int, string>(
                    null!,
                    (left, right) => "result",
                    rightStore));
        }

        [Fact]
        public void Constructor_WithNullJoinFunction_ShouldThrowArgumentNullException()
        {
            // Arrange
            var rightStore = new InMemoryStateStore<int, string>("RightStore");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new StreamTableLeftJoinOperator<int, string, int, string>(
                    left => left,
                    null!,
                    rightStore));
        }

        [Fact]
        public void Constructor_WithNullStateStore_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new StreamTableLeftJoinOperator<int, string, int, string>(
                    left => left,
                    (left, right) => "result",
                    null!));
        }

        #endregion

        #region GetStateStores Tests

        [Fact]
        public void GetStateStores_ShouldReturnRightStateStore()
        {
            // Arrange
            var rightStore = new InMemoryStateStore<int, string>("RightStore");
            var joinOperator = new StreamTableLeftJoinOperator<int, string, int, string>(
                left => left,
                (left, right) => "result",
                rightStore);

            // Act
            var stateStores = joinOperator.GetStateStores().ToList();

            // Assert
            Assert.Single(stateStores);
            Assert.Same(rightStore, stateStores[0]);
        }

        #endregion

        #region Type Mismatch Tests

        [Fact]
        public void Process_WithInvalidInputType_ShouldBeIgnored()
        {
            // Arrange
            var rightStore = new InMemoryStateStore<int, string>("RightStore");
            rightStore.Put(1, "Customer1");

            var results = new List<string>();
            var joinOperator = new StreamTableLeftJoinOperator<int, string, int, string>(
                left => left,
                (left, right) => $"Result:{left}",
                rightStore);

            joinOperator.SetNext(new SinkOperator<string>(x => results.Add(x)));

            // Act
            joinOperator.Process("invalid string");  // Wrong type
            joinOperator.Process(1);                 // Correct type

            // Assert
            Assert.Single(results); // Only the valid input produced a result
            Assert.Equal("Result:1", results[0]);
        }

        #endregion

        #region Integration with StreamBuilder Tests

        [Fact]
        public void StreamBuilder_LeftJoin_ShouldWorkInPipeline()
        {
            // Arrange
            var customerStore = new InMemoryStateStore<int, string>("CustomerStore");
            customerStore.Put(1, "Alice");
            customerStore.Put(2, "Bob");

            var results = new List<string>();

            var stream = StreamBuilder<int>.CreateNewStream("LeftJoinTestStream")
                .Stream()
                .LeftJoin(
                    customerStore,
                    orderId => orderId,
                    (orderId, customerName) => $"Order:{orderId}-Customer:{customerName ?? "Unknown"}")
                .Sink(x => results.Add(x))
                .Build();

            // Act
            stream.Start();
            stream.Emit(1);  // Alice
            stream.Emit(3);  // Unknown (no match)
            stream.Emit(2);  // Bob

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal("Order:1-Customer:Alice", results[0]);
            Assert.Equal("Order:3-Customer:Unknown", results[1]);
            Assert.Equal("Order:2-Customer:Bob", results[2]);
        }

        [Fact]
        public void StreamBuilder_LeftJoin_WithFilterAndMap_ShouldWorkInPipeline()
        {
            // Arrange
            var customerStore = new InMemoryStateStore<int, string>("CustomerStore");
            customerStore.Put(1, "Alice");
            customerStore.Put(2, "Bob");

            var results = new List<string>();

            var stream = StreamBuilder<int>.CreateNewStream("ComplexLeftJoinStream")
                .Stream()
                .Filter(x => x > 0)
                .LeftJoin(
                    customerStore,
                    orderId => orderId,
                    (orderId, customerName) => new { OrderId = orderId, Customer = customerName })
                .Map(x => $"{x.OrderId}:{x.Customer ?? "N/A"}")
                .Sink(x => results.Add(x))
                .Build();

            // Act
            stream.Start();
            stream.Emit(0);  // Filtered out
            stream.Emit(1);  // Alice
            stream.Emit(5);  // N/A (no match)

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal("1:Alice", results[0]);
            Assert.Equal("5:N/A", results[1]);
        }

        #endregion

        #region Concurrent Access Tests

        [Fact]
        public async Task LeftJoin_WithConcurrentAccess_ShouldBeThreadSafe()
        {
            // Arrange
            var rightStore = new InMemoryStateStore<int, string>("RightStore");
            for (int i = 0; i < 100; i++)
            {
                rightStore.Put(i, $"Customer{i}");
            }

            var results = new System.Collections.Concurrent.ConcurrentBag<string>();
            var joinOperator = new StreamTableLeftJoinOperator<int, string, int, string>(
                left => left,
                (left, right) => $"{left}:{right ?? "NULL"}",
                rightStore);

            joinOperator.SetNext(new SinkOperator<string>(x => results.Add(x)));

            // Act - Process from multiple threads
            var tasks = Enumerable.Range(0, 200).Select(i =>
                Task.Run(() => joinOperator.Process(i % 150))) // Some will match, some won't
                .ToArray();

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(200, results.Count);
            
            // Verify some have matches and some don't
            var matched = results.Count(r => !r.Contains("NULL"));
            var unmatched = results.Count(r => r.Contains("NULL"));
            Assert.True(matched > 0, "Should have some matched results");
            Assert.True(unmatched > 0, "Should have some unmatched results");
        }

        #endregion

        #region Telemetry Tests

        [Fact]
        public void LeftJoin_WithTelemetry_ShouldTrackMatchedAndUnmatchedCounters()
        {
            // Arrange
            var (mockProvider, state) = CreateMockTelemetryProvider();

            var rightStore = new InMemoryStateStore<int, string>("RightStore");
            rightStore.Put(1, "Customer1");
            rightStore.Put(2, "Customer2");

            var joinOperator = new StreamTableLeftJoinOperator<int, string, int, string>(
                left => left,
                (left, right) => $"{left}:{right ?? "NULL"}",
                rightStore);

            joinOperator.SetTelemetryProvider(mockProvider.Object);
            joinOperator.SetNext(new SinkOperator<string>(_ => { }));

            // Act
            joinOperator.Process(1);  // Matched
            joinOperator.Process(2);  // Matched
            joinOperator.Process(99); // Unmatched

            // Assert
            var processedCount = state.GetCounterValue("stream_table_left_join_processed_Int32");
            var matchedCount = state.GetCounterValue("stream_table_left_join_matched_Int32");
            var unmatchedCount = state.GetCounterValue("stream_table_left_join_unmatched_Int32");

            Assert.Equal(3, processedCount);
            Assert.Equal(2, matchedCount);
            Assert.Equal(1, unmatchedCount);
        }

        #region Mock Telemetry Infrastructure

        private static (Mock<ITelemetryProvider> provider, MockTelemetryState state) CreateMockTelemetryProvider()
        {
            var state = new MockTelemetryState();
            var mockProvider = new Mock<ITelemetryProvider>();
            var mockMetricsProvider = new Mock<IMetricsProvider>();
            var mockTracingProvider = new Mock<ITracingProvider>();

            mockMetricsProvider.Setup(m => m.CreateCounter(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string name, string desc) => new MockCounter(name, state));

            mockMetricsProvider.Setup(m => m.CreateHistogram(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string name, string desc) => new MockHistogram(name, state));

            mockTracingProvider.Setup(t => t.GetTracer(It.IsAny<string>()))
                .Returns((string name) => new MockTracer(name, state));

            mockProvider.Setup(p => p.GetMetricsProvider()).Returns(mockMetricsProvider.Object);
            mockProvider.Setup(p => p.GetTracingProvider()).Returns(mockTracingProvider.Object);

            return (mockProvider, state);
        }

        private class MockTelemetryState
        {
            private readonly object _lock = new();
            public Dictionary<string, double> CounterValues { get; } = new();

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
        }

        private class MockCounter : ICounter
        {
            private readonly string _name;
            private readonly MockTelemetryState _state;

            public MockCounter(string name, MockTelemetryState state)
            {
                _name = name;
                _state = state;
                _state.IncrementCounter(name, 0);
            }

            public void Increment(double value = 1) => _state.IncrementCounter(_name, value);
        }

        private class MockHistogram : IHistogram
        {
            public MockHistogram(string name, MockTelemetryState state) { }
            public void Record(double value) { }
        }

        private class MockTracer : ITracer
        {
            public MockTracer(string name, MockTelemetryState state) { }
            public ISpan StartSpan(string name) => new MockSpan();
        }

        private class MockSpan : ISpan
        {
            public void SetAttribute(string key, string value) { }
            public void AddEvent(string name, IDictionary<string, object>? attributes = null) { }
            public void Dispose() { }
        }

        #endregion

        #endregion

        #region Error Handling Tests

        [Fact]
        public void LeftJoin_WithErrorHandler_ShouldContinueOnError()
        {
            // Arrange
            var rightStore = new InMemoryStateStore<int, string>("RightStore");
            rightStore.Put(1, "Customer1");

            var results = new List<string>();
            var errors = new List<Exception>();

            var joinOperator = new StreamTableLeftJoinOperator<int, string, int, string>(
                left =>
                {
                    if (left == 0) throw new InvalidOperationException("Cannot process zero");
                    return left;
                },
                (left, right) => $"{left}:{right ?? "NULL"}",
                rightStore);

            var options = new StreamExecutionOptions
            {
                OnError = (ctx) =>
                {
                    errors.Add(ctx.Exception);
                    return ErrorHandlingDecision.Skip;
                }
            };

            joinOperator.SetErrorHandling(options);
            joinOperator.SetNext(new SinkOperator<string>(x => results.Add(x)));

            // Act
            joinOperator.Process(1);  // Success
            joinOperator.Process(0);  // Error - should be skipped
            joinOperator.Process(2);  // Success (no match)

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Single(errors);
            Assert.IsType<InvalidOperationException>(errors[0]);
        }

        #endregion
    }
}
