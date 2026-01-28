using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Cortex.Streams.Performance;
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

        /// <summary>
        /// Configure performance options for the stream, including buffered processing and backpressure.
        /// This enables async processing with <see cref="IStream{TIn, TCurrent}.EmitAsync"/> and 
        /// <see cref="IStream{TIn, TCurrent}.EmitAndForget"/> methods.
        /// </summary>
        /// <param name="performanceOptions">Performance options controlling buffer size, backpressure strategy, and concurrency.</param>
        /// <returns>The initial builder for chaining.</returns>
        /// <remarks>
        /// When performance options with buffered processing are not configured, the stream uses 
        /// synchronous processing for <see cref="IStream{TIn, TCurrent}.Emit"/> and simple Task.Run 
        /// for <see cref="IStream{TIn, TCurrent}.EmitAsync"/>, maintaining backward compatibility.
        /// </remarks>
        IInitialStreamBuilder<TIn> WithPerformanceOptions(StreamPerformanceOptions performanceOptions);

    }
}
