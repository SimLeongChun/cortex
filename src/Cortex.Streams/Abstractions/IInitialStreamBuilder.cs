using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Cortex.Telemetry;
using System;

namespace Cortex.Streams.Abstractions
{
    /// <summary>
    /// Initial builder interface for creating a stream processing pipeline.
    /// </summary>
    /// <typeparam name="TIn">The type of the initial input to the stream.</typeparam>
    public interface IInitialStreamBuilder<TIn>
    {
        /// <summary>
        /// Start the stream inside the application, in-app streaming
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        IStreamBuilder<TIn, TIn> Stream();

        /// <summary>
        /// Start configuring the Stream
        /// </summary>
        /// <param name="sourceOperator">Type of the Source Operator</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        IStreamBuilder<TIn, TIn> Stream(ISourceOperator<TIn> sourceOperator);

        /// <summary>
        /// Configure Telemetry for the Stream
        /// </summary>
        /// <param name="telemetryProvider">Telemetry provider like OpenTelemetryProvider</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        IInitialStreamBuilder<TIn> WithTelemetry(ITelemetryProvider telemetryProvider);


        /// <summary>
        /// Configure global error handling for the stream.
        /// </summary>
        /// <param name="executionOptions">Execution options controlling error handling strategy and callbacks.</param>
        /// <returns>The initial builder for chaining.</returns>
        IInitialStreamBuilder<TIn> WithErrorHandling(StreamExecutionOptions executionOptions);

    }
}
