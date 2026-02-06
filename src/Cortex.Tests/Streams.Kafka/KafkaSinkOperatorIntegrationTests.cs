using Confluent.Kafka;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Kafka;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Cortex.Tests.Streams.Kafka
{
    /// <summary>
    /// Integration tests for KafkaSinkOperator to diagnose message production issues.
    /// </summary>
    public class KafkaSinkOperatorIntegrationTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _bootstrapServers = "145.239.0.42:29092";
        private readonly string _topic = "cortex.events_tests";

        public KafkaSinkOperatorIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact(Skip = "Integration test - requires live Kafka")]
        public void Process_ProducesMessage_WhenStartIsCalled()
        {
            // Arrange
            var logger = new TestLogger<KafkaSinkOperator<string>>(_output);
            var sinkOperator = new KafkaSinkOperator<string>(
                bootstrapServers: _bootstrapServers,
                topic: _topic,
                logger: logger);

            // Act - Start is REQUIRED before processing
            sinkOperator.Start();
            
            var testMessage = $"Test message at {DateTime.UtcNow:O}";
            _output.WriteLine($"Sending message: {testMessage}");
            
            sinkOperator.Process(testMessage);
            
            // Give Kafka time to produce
            Thread.Sleep(2000);
            
            // Stop to flush pending messages
            sinkOperator.Stop();
            sinkOperator.Dispose();

            // Assert
            _output.WriteLine("Message should be produced to Kafka");
        }

        [Fact]
        public void Process_DoesNotProduceMessage_WhenStartIsNotCalled()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<KafkaSinkOperator<string>>>();
            var sinkOperator = new KafkaSinkOperator<string>(
                bootstrapServers: _bootstrapServers,
                topic: _topic,
                logger: mockLogger.Object);

            // Act - DO NOT call Start()
            var testMessage = "This message should not be produced";
            sinkOperator.Process(testMessage);

            // Assert - Should log warning
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("not running")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

            sinkOperator.Dispose();
        }

        [Fact(Skip = "Integration test - requires live Kafka")]
        public void Process_WithErrorHandling_ProducesMessages()
        {
            // Arrange
            var logger = new TestLogger<KafkaSinkOperator<string>>(_output);
            var sinkOperator = new KafkaSinkOperator<string>(
                bootstrapServers: _bootstrapServers,
                topic: _topic,
                logger: logger);

            // Configure error handling
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Retry,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromMilliseconds(100),
                OnError = ctx =>
                {
                    _output.WriteLine($"Error in {ctx.OperatorName}: {ctx.Exception.Message}");
                    return ErrorHandlingDecision.Retry;
                }
            };
            sinkOperator.SetErrorHandling(executionOptions);

            // Act
            sinkOperator.Start();

            for (int i = 0; i < 5; i++)
            {
                var message = $"Message {i} at {DateTime.UtcNow:O}";
                _output.WriteLine($"Producing: {message}");
                sinkOperator.Process(message);
            }

            // Give time for async production
            Thread.Sleep(3000);

            sinkOperator.Stop();
            sinkOperator.Dispose();

            // Assert
            _output.WriteLine("All 5 messages should be produced to Kafka");
        }

        [Fact(Skip = "Integration test - requires live Kafka")]
        public void Process_WithNullMessages_SkipsNulls()
        {
            // Arrange
            var logger = new TestLogger<KafkaSinkOperator<string>>(_output);
            var sinkOperator = new KafkaSinkOperator<string>(
                bootstrapServers: _bootstrapServers,
                topic: _topic,
                logger: logger);

            // Act
            sinkOperator.Start();

            sinkOperator.Process("Valid message 1");
            sinkOperator.Process(null); // Should skip
            sinkOperator.Process("Valid message 2");

            Thread.Sleep(2000);

            sinkOperator.Stop();
            sinkOperator.Dispose();

            // Assert
            _output.WriteLine("Only 2 messages should be produced (nulls are skipped)");
        }

        [Fact(Skip = "Integration test - requires live Kafka")]
        public void Process_WithCustomConfig_ProducesMessages()
        {
            // Arrange
            var config = new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true,
                MaxInFlight = 5,
                LingerMs = 0, // Send immediately for testing
                CompressionType = CompressionType.None
            };

            var logger = new TestLogger<KafkaSinkOperator<string>>(_output);
            var sinkOperator = new KafkaSinkOperator<string>(
                bootstrapServers: _bootstrapServers,
                topic: _topic,
                config: config,
                logger: logger);

            // Act
            sinkOperator.Start();

            var message = $"Custom config test at {DateTime.UtcNow:O}";
            _output.WriteLine($"Producing: {message}");
            sinkOperator.Process(message);

            Thread.Sleep(2000);

            sinkOperator.Stop();
            sinkOperator.Dispose();

            // Assert
            _output.WriteLine("Message should be produced with custom config");
        }

        [Fact]
        public void Start_CanBeCalledMultipleTimes_WithoutError()
        {
            // Arrange
            var sinkOperator = new KafkaSinkOperator<string>(
                bootstrapServers: _bootstrapServers,
                topic: _topic);

            // Act - Multiple starts should be safe
            sinkOperator.Start();
            sinkOperator.Start(); // Should not throw

            // Assert
            sinkOperator.Process("test");
            
            sinkOperator.Stop();
            sinkOperator.Dispose();
        }

        [Fact]
        public void Dispose_AfterStart_FlushesMessages()
        {
            // Arrange
            var logger = new TestLogger<KafkaSinkOperator<string>>(_output);
            var sinkOperator = new KafkaSinkOperator<string>(
                bootstrapServers: _bootstrapServers,
                topic: _topic,
                logger: logger);

            // Act
            sinkOperator.Start();
            sinkOperator.Process("Message before dispose");

            // Dispose should flush
            sinkOperator.Dispose();

            // Assert - Should not throw
            Assert.Throws<ObjectDisposedException>(() => sinkOperator.Process("After dispose"));
        }

        [Fact(Skip = "Integration test - requires live Kafka")]
        public void FullStreamTest_WithKafkaSink()
        {
            // Arrange
            var logger = new TestLogger<KafkaSinkOperator<string>>(_output);
            var sinkOperator = new KafkaSinkOperator<string>(
                bootstrapServers: _bootstrapServers,
                topic: _topic,
                logger: logger);

            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Skip,
                OnError = ctx =>
                {
                    _output.WriteLine($"ERROR: {ctx.Exception.Message}");
                    return ErrorHandlingDecision.Skip;
                }
            };

            sinkOperator.SetErrorHandling(executionOptions);

            // Act - Simulate stream processing
            sinkOperator.Start();

            _output.WriteLine("=== Starting message production ===");

            for (int i = 0; i < 10; i++)
            {
                var message = $"Stream message {i} at {DateTime.UtcNow:O}";
                _output.WriteLine($"Processing: {message}");
                sinkOperator.Process(message);
                Thread.Sleep(100); // Small delay between messages
            }

            _output.WriteLine("=== Stopping and flushing ===");
            sinkOperator.Stop();

            _output.WriteLine("=== Disposing ===");
            sinkOperator.Dispose();

            _output.WriteLine("=== Test complete ===");
        }

        [Fact(Skip = "Integration test - requires live Kafka")]
        public void DiagnosticTest_CheckWhatIsHappening()
        {
            // This test helps diagnose exactly what's happening with the new implementation
            var logger = new TestLogger<KafkaSinkOperator<string>>(_output);
            
            _output.WriteLine("=== Creating KafkaSinkOperator ===");
            var sinkOperator = new KafkaSinkOperator<string>(
                bootstrapServers: _bootstrapServers,
                topic: _topic,
                logger: logger);

            _output.WriteLine("=== Setting Error Handling ===");
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Retry,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1),
                OnError = ctx =>
                {
                    _output.WriteLine($"[ERROR HANDLER] Operator: {ctx.OperatorName}");
                    _output.WriteLine($"[ERROR HANDLER] Exception: {ctx.Exception?.Message}");
                    _output.WriteLine($"[ERROR HANDLER] Attempt: {ctx.Attempt}");
                    return ErrorHandlingDecision.Retry;
                }
            };
            sinkOperator.SetErrorHandling(executionOptions);

            _output.WriteLine("=== Starting Operator (THIS IS CRITICAL!) ===");
            sinkOperator.Start();

            _output.WriteLine("=== Processing Test Message ===");
            var testMessage = $"Diagnostic test at {DateTime.UtcNow:O}";
            _output.WriteLine($"Message content: {testMessage}");
            
            try
            {
                sinkOperator.Process(testMessage);
                _output.WriteLine("Process() completed without exception");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Process() threw exception: {ex.GetType().Name}: {ex.Message}");
            }

            _output.WriteLine("=== Waiting 5 seconds for async production ===");
            Thread.Sleep(5000);

            _output.WriteLine("=== Stopping (flushes pending messages) ===");
            try
            {
                sinkOperator.Stop();
                _output.WriteLine("Stop() completed");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Stop() threw exception: {ex.GetType().Name}: {ex.Message}");
            }

            _output.WriteLine("=== Disposing ===");
            sinkOperator.Dispose();

            _output.WriteLine("=== Test Complete ===");
            _output.WriteLine("");
            _output.WriteLine("Check Kafka topic 'cortex.events_tests' for the message.");
        }
    }

    /// <summary>
    /// Test logger that outputs to xUnit test output.
    /// </summary>
    internal class TestLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}] {message}");
            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception}");
            }
        }
    }
}
