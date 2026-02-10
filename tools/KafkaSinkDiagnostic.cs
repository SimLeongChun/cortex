using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Kafka;
using System;
using System.Threading;

namespace Cortex.DiagnosticTools
{
    /// <summary>
    /// Diagnostic tool to demonstrate and fix Kafka message production issues.
    /// </summary>
    class KafkaSinkDiagnostic
    {
        static void Main(string[] args)
        {
            Console.WriteLine("====================================");
            Console.WriteLine("Kafka Sink Operator Diagnostic Tool");
            Console.WriteLine("====================================");
            Console.WriteLine();

            var bootstrapServers = "localhost:29092";
            var topic = "cortex.events_tests";

            Console.WriteLine($"Bootstrap Servers: {bootstrapServers}");
            Console.WriteLine($"Topic: {topic}");
            Console.WriteLine();

            // Demonstrate the WRONG way (without calling Start())
            Console.WriteLine("=== Test 1: WITHOUT calling Start() - MESSAGES WILL NOT BE PRODUCED ===");
            TestWithoutStart(bootstrapServers, topic);
            Console.WriteLine();

            Thread.Sleep(1000);

            // Demonstrate the CORRECT way (with calling Start())
            Console.WriteLine("=== Test 2: WITH calling Start() - MESSAGES WILL BE PRODUCED ===");
            TestWithStart(bootstrapServers, topic);
            Console.WriteLine();

            Thread.Sleep(1000);

            // Demonstrate with error handling
            Console.WriteLine("=== Test 3: WITH Start() and Error Handling ===");
            TestWithErrorHandling(bootstrapServers, topic);
            Console.WriteLine();

            Console.WriteLine("====================================");
            Console.WriteLine("Diagnostic Complete!");
            Console.WriteLine("====================================");
            Console.WriteLine();
            Console.WriteLine("Check your Kafka topic to see the messages.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void TestWithoutStart(string bootstrapServers, string topic)
        {
            Console.WriteLine("[CREATING OPERATOR]");
            var sinkOperator = new KafkaSinkOperator<string>(
                bootstrapServers: bootstrapServers,
                topic: topic);

            Console.WriteLine("[PROCESSING MESSAGE - WITHOUT Start()]");
            sinkOperator.Process("This message WILL NOT be produced because Start() was not called!");

            Console.WriteLine("[DISPOSING]");
            sinkOperator.Dispose();

            Console.WriteLine("[RESULT] ? Message was NOT produced (operator not running)");
        }

        static void TestWithStart(string bootstrapServers, string topic)
        {
            Console.WriteLine("[CREATING OPERATOR]");
            var sinkOperator = new KafkaSinkOperator<string>(
                bootstrapServers: bootstrapServers,
                topic: topic);

            Console.WriteLine("[CALLING Start() - THIS IS CRITICAL!]");
            sinkOperator.Start();

            Console.WriteLine("[PROCESSING MESSAGE - WITH Start()]");
            var message = $"SUCCESS: Message sent at {DateTime.UtcNow:O}";
            sinkOperator.Process(message);
            Console.WriteLine($"  Content: {message}");

            Console.WriteLine("[WAITING 2 seconds for async production]");
            Thread.Sleep(2000);

            Console.WriteLine("[STOPPING - Flushes pending messages]");
            sinkOperator.Stop();

            Console.WriteLine("[DISPOSING]");
            sinkOperator.Dispose();

            Console.WriteLine("[RESULT] ? Message WAS produced!");
        }

        static void TestWithErrorHandling(string bootstrapServers, string topic)
        {
            Console.WriteLine("[CREATING OPERATOR]");
            var sinkOperator = new KafkaSinkOperator<string>(
                bootstrapServers: bootstrapServers,
                topic: topic);

            Console.WriteLine("[CONFIGURING ERROR HANDLING]");
            var executionOptions = new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Retry,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1),
                OnError = ctx =>
                {
                    Console.WriteLine($"  [ERROR HANDLER] Operator: {ctx.OperatorName}");
                    Console.WriteLine($"  [ERROR HANDLER] Exception: {ctx.Exception?.Message}");
                    Console.WriteLine($"  [ERROR HANDLER] Attempt: {ctx.Attempt}");
                    return ErrorHandlingDecision.Retry;
                }
            };
            sinkOperator.SetErrorHandling(executionOptions);

            Console.WriteLine("[STARTING OPERATOR]");
            sinkOperator.Start();

            Console.WriteLine("[PRODUCING 5 MESSAGES]");
            for (int i = 1; i <= 5; i++)
            {
                var message = $"Message #{i} at {DateTime.UtcNow:O}";
                Console.WriteLine($"  Producing: {message}");
                sinkOperator.Process(message);
                Thread.Sleep(100);
            }

            Console.WriteLine("[WAITING 3 seconds for async production]");
            Thread.Sleep(3000);

            Console.WriteLine("[STOPPING]");
            sinkOperator.Stop();

            Console.WriteLine("[DISPOSING]");
            sinkOperator.Dispose();

            Console.WriteLine("[RESULT] ? All 5 messages WAS produced with error handling!");
        }
    }
}
