using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using Cortex.Telemetry;
using System;

namespace Cortex.Streams.Abstractions
{
    public interface IInitialStreamBuilder<TIn, TCurrent>
    {
        /// <summary>
        /// Start the stream inside the application, in-app streaming
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        IStreamBuilder<TIn, TCurrent> Stream();

        /// <summary>
        /// Start configuring the Stream
        /// </summary>
        /// <param name="sourceOperator">Type of the Source Operator</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        IStreamBuilder<TIn, TCurrent> Stream(ISourceOperator<TCurrent> sourceOperator);

        /// <summary>
        /// Configure Telemetry for the Stream
        /// </summary>
        /// <param name="telemetryProvider">Telemetry provider like OpenTelemetryProvider</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        IInitialStreamBuilder<TIn, TCurrent> WithTelemetry(ITelemetryProvider telemetryProvider);


        /// <summary>
        /// Configure global error handling for the stream.
        /// </summary>
        /// <param name="executionOptions">Execution options controlling error handling strategy and callbacks.</param>
        /// <returns>The initial builder for chaining.</returns>
        IInitialStreamBuilder<TIn, TCurrent> WithErrorHandling(StreamExecutionOptions executionOptions);

    }
}
