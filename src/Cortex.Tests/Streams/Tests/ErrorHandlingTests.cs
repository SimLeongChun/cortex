using Cortex.Streams;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;

namespace Cortex.Streams.Tests
{
    /// <summary>
    /// Comprehensive tests for error handling and resilience in Cortex.Streams.
    /// These tests verify production-grade error handling scenarios including:
    /// - Skip strategy (continue processing after errors)
    /// - Retry strategy (retry failed operations)
    /// - Stop strategy (graceful shutdown on errors)
    /// - Rethrow strategy (propagate exceptions)
    /// - Custom error handlers
    /// - Error context information
    /// - Retry delays and max retries
    /// </summary>
    public class ErrorHandlingTests
    {
        #region Skip Strategy Tests

        [Fact]
        public void SkipStrategy_ContinuesProcessingAfterError()
        {
            // Arrange
            var processedItems = new List<int>();
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Skip
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("SkipStrategyTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    if (x == 2) throw new InvalidOperationException("Simulated error on item 2");
                    return x * 10;
                })
                .Sink(x => processedItems.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);
            stream.Emit(2); // This should be skipped
            stream.Emit(3);
            stream.Emit(4);

            // Assert
            Assert.Equal(new[] { 10, 30, 40 }, processedItems);
        }

        [Fact]
        public void SkipStrategy_InFilterOperator_SkipsOnPredicateError()
        {
            // Arrange
            var processedItems = new List<int>();
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Skip
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("SkipFilterTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Filter(x =>
                {
                    if (x == 3) throw new InvalidOperationException("Filter error on 3");
                    return x > 0;
                })
                .Sink(x => processedItems.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);
            stream.Emit(2);
            stream.Emit(3); // Should be skipped due to error
            stream.Emit(4);

            // Assert
            Assert.Equal(new[] { 1, 2, 4 }, processedItems);
        }

        [Fact]
        public void SkipStrategy_InSinkOperator_SkipsFailedSink()
        {
            // Arrange
            var processedItems = new List<int>();
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Skip
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("SkipSinkTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x => x * 2)
                .Sink(x =>
                {
                    if (x == 6) throw new InvalidOperationException("Sink error on 6");
                    processedItems.Add(x);
                })
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);
            stream.Emit(2);
            stream.Emit(3); // Sink will throw when processing 6
            stream.Emit(4);

            // Assert
            Assert.Equal(new[] { 2, 4, 8 }, processedItems);
        }

        [Fact]
        public void SkipStrategy_InFlatMapOperator_SkipsFailedTransformation()
        {
            // Arrange
            var processedItems = new List<int>();
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Skip
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("SkipFlatMapTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .FlatMap(x =>
                {
                    if (x == 2) throw new InvalidOperationException("FlatMap error on 2");
                    return new[] { x, x * 10 };
                })
                .Sink(x => processedItems.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);
            stream.Emit(2); // Should be skipped
            stream.Emit(3);

            // Assert
            Assert.Equal(new[] { 1, 10, 3, 30 }, processedItems);
        }

        #endregion

        #region Retry Strategy Tests

        [Fact]
        public void RetryStrategy_RetriesFailedOperation()
        {
            // Arrange
            var attemptCount = 0;
            var processedItems = new List<int>();
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Retry,
                MaxRetries = 3,
                RetryDelay = TimeSpan.Zero
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("RetryTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    attemptCount++;
                    if (attemptCount < 3 && x == 1)
                        throw new InvalidOperationException("Transient error");
                    return x * 10;
                })
                .Sink(x => processedItems.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);

            // Assert
            Assert.Equal(3, attemptCount);
            Assert.Equal(new[] { 10 }, processedItems);
        }

        [Fact]
        public void RetryStrategy_StopsGracefully_WhenMaxRetriesExceeded()
        {
            // Arrange
            var attemptCount = 0;
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Retry,
                MaxRetries = 2,
                RetryDelay = TimeSpan.Zero
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("RetryExceededTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    attemptCount++;
                    throw new InvalidOperationException("Always fails");
#pragma warning disable CS0162
                    return x;
#pragma warning restore CS0162
                })
                .Sink(x => { })
                .Build();

            stream.Start();

            // Act - Emit will trigger retries then stop gracefully when max exceeded
            stream.Emit(1);

            // Assert - Stream should be stopped after max retries exceeded
            Assert.Equal(StreamStatuses.NOT_RUNNING, stream.GetStatus());
            Assert.Equal(2, attemptCount); // Initial + (MaxRetries - 1) retries
        }

        [Fact]
        public void RetryStrategy_RespectsRetryDelay()
        {
            // Arrange
            var timestamps = new List<DateTime>();
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Retry,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromMilliseconds(50)
            };

            var attemptCount = 0;
            var stream = StreamBuilder<int>
                .CreateNewStream("RetryDelayTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    timestamps.Add(DateTime.UtcNow);
                    attemptCount++;
                    if (attemptCount < 3)
                        throw new InvalidOperationException("Transient error");
                    return x;
                })
                .Sink(x => { })
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);

            // Assert - verify delays between retries
            Assert.Equal(3, timestamps.Count);
            for (int i = 1; i < timestamps.Count; i++)
            {
                var delay = timestamps[i] - timestamps[i - 1];
                Assert.True(delay >= TimeSpan.FromMilliseconds(40), $"Delay between attempt {i} and {i + 1} was {delay.TotalMilliseconds}ms");
            }
        }

        #endregion

        #region Stop Strategy Tests

        [Fact]
        public void StopStrategy_GracefullyStopsStreamAndStopsProcessing()
        {
            // Arrange
            var processedItems = new List<int>();
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Stop
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("StopTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    if (x == 2) throw new InvalidOperationException("Error on 2");
                    return x;
                })
                .Sink(x => processedItems.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1); // Success
            stream.Emit(2); // Error triggers graceful stop (exception is swallowed)
            
            // Assert - stream should be stopped
            Assert.Equal(StreamStatuses.NOT_RUNNING, stream.GetStatus());
            Assert.Single(processedItems); // Only item 1 was processed
        }

        [Fact]
        public void StopStrategy_StopsStreamAfterError()
        {
            // Arrange
            var processedItems = new List<int>();
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Stop
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("StopGracefulTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    if (x == 2) throw new InvalidOperationException("Error on 2");
                    return x;
                })
                .Sink(x => processedItems.Add(x))
                .Build();

            stream.Start();
            Assert.Equal(StreamStatuses.RUNNING, stream.GetStatus());

            // Act
            stream.Emit(1);
            stream.Emit(2); // This triggers graceful stop

            // Assert - stream is stopped, no exception propagates
            Assert.Equal(StreamStatuses.NOT_RUNNING, stream.GetStatus());
            Assert.Single(processedItems);
        }



        #endregion

        #region Rethrow Strategy Tests (Default Behavior)

        [Fact]
        public void RethrowStrategy_PropagatesOriginalException()
        {
            // Arrange - No error handling configured means Rethrow
            var stream = StreamBuilder<int>
                .CreateNewStream("RethrowTest")
                .Stream()
                .Map(x =>
                {
                    if (x == 2) throw new ArgumentException("Original exception");
                    return x;
                })
                .Sink(x => { })
                .Build();

            stream.Start();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => stream.Emit(2));
            Assert.Equal("Original exception", exception.Message);
        }

        [Fact]
        public void NoneStrategy_BehavesLikeRethrow()
        {
            // Arrange
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.None
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("NoneTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    if (x == 2) throw new ArgumentException("Test exception");
                    return x;
                })
                .Sink(x => { })
                .Build();

            stream.Start();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => stream.Emit(2));
        }

        #endregion

        #region Custom Error Handler Tests

        [Fact]
        public void CustomErrorHandler_CanDecidePerError()
        {
            // Arrange
            var processedItems = new List<int>();
            var errorContexts = new List<StreamErrorContext>();

            var executionOptions = new StreamExecutionOptions
            {
                OnError = ctx =>
                {
                    errorContexts.Add(ctx);
                    // Skip ArgumentExceptions, rethrow others
                    return ctx.Exception is ArgumentException
                        ? ErrorHandlingDecision.Skip
                        : ErrorHandlingDecision.Rethrow;
                }
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("CustomHandlerTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    if (x == 2) throw new ArgumentException("Skippable error");
                    if (x == 3) throw new InvalidOperationException("Fatal error");
                    return x * 10;
                })
                .Sink(x => processedItems.Add(x))
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);
            stream.Emit(2); // Should be skipped
            Assert.Throws<InvalidOperationException>(() => stream.Emit(3)); // Should rethrow

            // Assert
            Assert.Equal(new[] { 10 }, processedItems);
            Assert.Equal(2, errorContexts.Count);
        }

        [Fact]
        public void CustomErrorHandler_ReceivesCorrectContext()
        {
            // Arrange
            StreamErrorContext capturedContext = null;
            var executionOptions = new StreamExecutionOptions
            {
                OnError = ctx =>
                {
                    capturedContext = ctx;
                    return ErrorHandlingDecision.Skip;
                }
            };

            var stream = StreamBuilder<string>
                .CreateNewStream("ContextTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    throw new InvalidOperationException("Test error");
#pragma warning disable CS0162 // Unreachable code detected
                    return x.Length;
#pragma warning restore CS0162
                })
                .Sink(x => { })
                .Build();

            stream.Start();

            // Act
            stream.Emit("test");

            // Assert
            Assert.NotNull(capturedContext);
            Assert.Equal("ContextTest", capturedContext.StreamName);
            Assert.Contains("MapOperator", capturedContext.OperatorName);
            Assert.Equal("test", capturedContext.Input);
            Assert.IsType<InvalidOperationException>(capturedContext.Exception);
            Assert.Equal(1, capturedContext.Attempt);
        }

        [Fact]
        public void CustomErrorHandler_CanRetryWithAttemptTracking()
        {
            // Arrange
            var attempts = new List<int>();
            var executionOptions = new StreamExecutionOptions
            {
                MaxRetries = 5,
                OnError = ctx =>
                {
                    attempts.Add(ctx.Attempt);
                    return ctx.Attempt < 3 ? ErrorHandlingDecision.Retry : ErrorHandlingDecision.Skip;
                }
            };

            var attemptCount = 0;
            var stream = StreamBuilder<int>
                .CreateNewStream("AttemptTrackingTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    attemptCount++;
                    throw new InvalidOperationException("Always fails");
#pragma warning disable CS0162
                    return x;
#pragma warning restore CS0162
                })
                .Sink(x => { })
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);

            // Assert - Should have 3 attempts (1, 2, 3) then skip on attempt 3
            Assert.Equal(new[] { 1, 2, 3 }, attempts);
            Assert.Equal(3, attemptCount);
        }

        [Fact]
        public void CustomErrorHandler_CanForceStop()
        {
            // Arrange
            var executionOptions = new StreamExecutionOptions
            {
                OnError = ctx => ErrorHandlingDecision.Stop
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("ForceStopTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    throw new InvalidOperationException("Error");
#pragma warning disable CS0162
                    return x;
#pragma warning restore CS0162
                })
                .Sink(x => { })
                .Build();

            stream.Start();

            // Act - Emit swallows StreamStoppedException and stops the stream gracefully
            stream.Emit(1);

            // Assert - stream is stopped
            Assert.Equal(StreamStatuses.NOT_RUNNING, stream.GetStatus());
        }

        #endregion

        #region StreamStoppedException Tests

        [Fact]
        public void StreamStoppedException_CanBeCreatedWithInnerException()
        {
            // Arrange
            var innerException = new ArgumentException("Inner error");

            // Act
            var exception = new StreamStoppedException("Test message", innerException);

            // Assert
            Assert.NotNull(exception.InnerException);
            Assert.IsType<ArgumentException>(exception.InnerException);
            Assert.Equal("Inner error", exception.InnerException.Message);
            Assert.Equal("Test message", exception.Message);
        }

        [Fact]
        public void StopStrategy_StopsStreamOnError()
        {
            // Arrange
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Stop
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("OperatorNameTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    throw new InvalidOperationException("Error");
#pragma warning disable CS0162
                    return x.ToString();
#pragma warning restore CS0162
                })
                .Sink(x => { })
                .Build();

            stream.Start();
            Assert.Equal(StreamStatuses.RUNNING, stream.GetStatus());

            // Act
            stream.Emit(1); // Error should stop stream gracefully

            // Assert
            Assert.Equal(StreamStatuses.NOT_RUNNING, stream.GetStatus());
        }

        #endregion

        #region Error Handling Propagation Tests

        [Fact]
        public void ErrorHandling_PropagatesAcrossOperatorChain()
        {
            // Arrange
            var errors = new List<string>();
            var executionOptions = new StreamExecutionOptions
            {
                OnError = ctx =>
                {
                    errors.Add(ctx.OperatorName);
                    return ErrorHandlingDecision.Skip;
                }
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("PropagationTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    if (x == 1) throw new InvalidOperationException("Map error");
                    return x;
                })
                .Filter(x =>
                {
                    if (x == 2) throw new InvalidOperationException("Filter error");
                    return true;
                })
                .Map(x =>
                {
                    if (x == 3) throw new InvalidOperationException("Second map error");
                    return x * 10;
                })
                .Sink(x =>
                {
                    if (x == 40) throw new InvalidOperationException("Sink error");
                })
                .Build();

            stream.Start();

            // Act
            stream.Emit(1); // Error in first Map
            stream.Emit(2); // Error in Filter
            stream.Emit(3); // Error in second Map
            stream.Emit(4); // Error in Sink
            stream.Emit(5); // Success

            // Assert
            Assert.Equal(4, errors.Count);
            Assert.Contains(errors, e => e.Contains("MapOperator<Int32,Int32>"));
            Assert.Contains(errors, e => e.Contains("FilterOperator"));
            Assert.Contains(errors, e => e.Contains("SinkOperator"));
        }

        #endregion

        #region Edge Cases and Production Scenarios

        [Fact]
        public void ErrorHandling_HandlesNullInput()
        {
            // Arrange
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Skip
            };

            var processedItems = new List<string>();
            var stream = StreamBuilder<string>
                .CreateNewStream("NullInputTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x => x.ToUpper())
                .Sink(x => processedItems.Add(x))
                .Build();

            stream.Start();

            // Act & Assert - null input causes ArgumentException from MapOperator
            // which is NOT wrapped by error handling (input validation happens before)
            Assert.Throws<ArgumentException>(() => stream.Emit(null!));
        }

        [Fact]
        public void ErrorHandling_WorksWithMultipleStreams()
        {
            // Arrange
            var results1 = new List<int>();
            var results2 = new List<int>();

            var options1 = new StreamExecutionOptions { ErrorHandlingStrategy = ErrorHandlingStrategy.Skip };
            var options2 = new StreamExecutionOptions { ErrorHandlingStrategy = ErrorHandlingStrategy.Stop };

            var stream1 = StreamBuilder<int>
                .CreateNewStream("Stream1")
                .WithErrorHandling(options1)
                .Stream()
                .Map(x =>
                {
                    if (x == 2) throw new InvalidOperationException("Error");
                    return x;
                })
                .Sink(x => results1.Add(x))
                .Build();

            var stream2 = StreamBuilder<int>
                .CreateNewStream("Stream2")
                .WithErrorHandling(options2)
                .Stream()
                .Map(x =>
                {
                    if (x == 2) throw new InvalidOperationException("Error");
                    return x;
                })
                .Sink(x => results2.Add(x))
                .Build();

            stream1.Start();
            stream2.Start();

            // Act
            stream1.Emit(1);
            stream1.Emit(2); // Skipped
            stream1.Emit(3);

            stream2.Emit(1);
            stream2.Emit(2); // Stops gracefully (exception is swallowed)

            // Assert
            Assert.Equal(new[] { 1, 3 }, results1);
            Assert.Equal(new[] { 1 }, results2);
            Assert.Equal(StreamStatuses.NOT_RUNNING, stream2.GetStatus());
        }

        [Fact]
        public void ErrorHandling_RetryWithZeroMaxRetries_StopsGracefully()
        {
            // Arrange
            var attemptCount = 0;
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Retry,
                MaxRetries = 0 // No retries allowed
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("ZeroRetriesTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    attemptCount++;
                    throw new InvalidOperationException("Always fails");
#pragma warning disable CS0162
                    return x;
#pragma warning restore CS0162
                })
                .Sink(x => { })
                .Build();

            stream.Start();

            // Act - max retries exceeded immediately, stream stops gracefully
            stream.Emit(1);

            // Assert
            Assert.Equal(StreamStatuses.NOT_RUNNING, stream.GetStatus());
            Assert.Equal(1, attemptCount); // Only initial attempt, no retries
        }

        [Fact]
        public void ErrorHandling_StopStrategy_StopsStreamGracefully()
        {
            // Arrange
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Stop
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("StackTraceTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x => ThrowNestedException())
                .Sink(x => { })
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);

            // Assert - stream should be stopped
            Assert.Equal(StreamStatuses.NOT_RUNNING, stream.GetStatus());
        }

        private static int ThrowNestedException()
        {
            throw new InvalidOperationException("Nested exception");
        }

        [Fact]
        public void ErrorHandling_WorksWithComplexPipeline()
        {
            // Arrange
            var results = new List<string>();
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Skip
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("ComplexPipelineTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Filter(x =>
                {
                    if (x == 1) throw new InvalidOperationException("Filter error");
                    return x % 2 == 0;
                })
                .Map(x =>
                {
                    if (x == 4) throw new InvalidOperationException("Map error");
                    return x * 10;
                })
                .FlatMap(x =>
                {
                    if (x == 60) throw new InvalidOperationException("FlatMap error");
                    return new[] { x, x + 1 };
                })
                .Map(x => $"Value: {x}")
                .Sink(x =>
                {
                    if (x.Contains("81")) throw new InvalidOperationException("Sink error");
                    results.Add(x);
                })
                .Build();

            stream.Start();

            // Act
            stream.Emit(1);  // Filter error - skipped
            stream.Emit(2);  // OK: 2 -> 20 -> [20, 21] -> "Value: 20", "Value: 21"
            stream.Emit(3);  // Filtered out (odd)
            stream.Emit(4);  // Map error - skipped
            stream.Emit(5);  // Filtered out (odd)
            stream.Emit(6);  // FlatMap error - skipped
            stream.Emit(8);  // OK: 8 -> 80 -> [80, 81] -> "Value: 80", sink error on "Value: 81"

            // Assert
            Assert.Equal(new[] { "Value: 20", "Value: 21", "Value: 80" }, results);
        }

        [Fact]
        public async Task ErrorHandling_WorksWithAsyncEmit()
        {
            // Arrange
            var processedItems = new List<int>();
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Skip
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("AsyncEmitTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    if (x == 2) throw new InvalidOperationException("Error");
                    return x * 10;
                })
                .Sink(x =>
                {
                    lock (processedItems)
                    {
                        processedItems.Add(x);
                    }
                })
                .Build();

            stream.Start();

            // Act
            var tasks = new[]
            {
                stream.EmitAsync(1),
                stream.EmitAsync(2),  // Error - skipped
                stream.EmitAsync(3)
            };

            await Task.WhenAll(tasks);

            // Assert - order may vary due to async
            processedItems.Sort();
            Assert.Equal(new[] { 10, 30 }, processedItems);
        }


        #endregion

        #region StreamErrorContext Tests

        [Fact]
        public void StreamErrorContext_StoresAllProperties()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error");

            // Act
            var context = new StreamErrorContext(
                streamName: "TestStream",
                operatorName: "MapOperator",
                input: "test input",
                exception: exception,
                attempt: 3);

            // Assert
            Assert.Equal("TestStream", context.StreamName);
            Assert.Equal("MapOperator", context.OperatorName);
            Assert.Equal("test input", context.Input);
            Assert.Same(exception, context.Exception);
            Assert.Equal(3, context.Attempt);
        }

        #endregion

        #region StreamExecutionOptions Tests

        [Fact]
        public void StreamExecutionOptions_HasCorrectDefaults()
        {
            // Act
            var options = new StreamExecutionOptions();

            // Assert
            Assert.Equal(ErrorHandlingStrategy.None, options.ErrorHandlingStrategy);
            Assert.Equal(3, options.MaxRetries);
            Assert.Equal(TimeSpan.Zero, options.RetryDelay);
            Assert.Null(options.OnError);
        }

        [Fact]
        public void StreamExecutionOptions_CanBeConfigured()
        {
            // Arrange & Act
            var options = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Retry,
                MaxRetries = 5,
                RetryDelay = TimeSpan.FromSeconds(2),
                OnError = ctx => ErrorHandlingDecision.Skip
            };

            // Assert
            Assert.Equal(ErrorHandlingStrategy.Retry, options.ErrorHandlingStrategy);
            Assert.Equal(5, options.MaxRetries);
            Assert.Equal(TimeSpan.FromSeconds(2), options.RetryDelay);
            Assert.NotNull(options.OnError);
        }

        #endregion

        #region Operator-Level Error Handling Interface Tests

        [Fact]
        public void MapOperator_ImplementsIErrorHandlingEnabled()
        {
            // Arrange
            var mapOperator = new MapOperator<int, int>(x => x * 2);

            // Act & Assert
            Assert.IsAssignableFrom<IErrorHandlingEnabled>(mapOperator);
        }

        [Fact]
        public void FilterOperator_ImplementsIErrorHandlingEnabled()
        {
            // Arrange
            var filterOperator = new FilterOperator<int>(x => x > 0);

            // Act & Assert
            Assert.IsAssignableFrom<IErrorHandlingEnabled>(filterOperator);
        }

        [Fact]
        public void SinkOperator_ImplementsIErrorHandlingEnabled()
        {
            // Arrange
            var sinkOperator = new SinkOperator<int>(x => { });

            // Act & Assert
            Assert.IsAssignableFrom<IErrorHandlingEnabled>(sinkOperator);
        }

        [Fact]
        public void FlatMapOperator_ImplementsIErrorHandlingEnabled()
        {
            // Arrange
            var flatMapOperator = new FlatMapOperator<int, int>(x => new[] { x });

            // Act & Assert
            Assert.IsAssignableFrom<IErrorHandlingEnabled>(flatMapOperator);
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task ErrorHandling_IsThreadSafe_UnderConcurrentEmits()
        {
            // Arrange
            var processedCount = 0;
            var errorCount = 0;
            var executionOptions = new StreamExecutionOptions
            {
                OnError = ctx =>
                {
                    Interlocked.Increment(ref errorCount);
                    return ErrorHandlingDecision.Skip;
                }
            };

            var stream = StreamBuilder<int>
                .CreateNewStream("ThreadSafetyTest")
                .WithErrorHandling(executionOptions)
                .Stream()
                .Map(x =>
                {
                    if (x % 3 == 0) throw new InvalidOperationException("Error");
                    return x;
                })
                .Sink(x => Interlocked.Increment(ref processedCount))
                .Build();

            stream.Start();

            // Act - emit 100 items concurrently
            var tasks = Enumerable.Range(1, 100)
                .Select(i => Task.Run(() => stream.Emit(i)))
                .ToArray();

            await Task.WhenAll(tasks);

            // Assert
            // Items 3, 6, 9, ..., 99 (33 items) should error
            // Items not divisible by 3 (67 items) should succeed
            Assert.Equal(33, errorCount);
            Assert.Equal(67, processedCount);
        }

        #endregion
    }
}
