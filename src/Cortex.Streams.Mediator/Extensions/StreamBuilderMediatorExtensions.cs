using Cortex.Mediator;
using Cortex.Mediator.Commands;
using Cortex.Mediator.Notifications;
using Cortex.Mediator.Queries;
using Cortex.Mediator.Streaming;
using Cortex.Streams.Abstractions;
using Cortex.Streams.Mediator.Operators;
using System;
using System.Threading;

namespace Cortex.Streams.Mediator.Extensions
{
    /// <summary>
    /// Extension methods for integrating Cortex.Mediator with Cortex.Streams.
    /// Provides fluent API for stream builders to interact with mediator patterns.
    /// </summary>
    public static class StreamBuilderMediatorExtensions
    {
        #region Command Sinks

        /// <summary>
        /// Adds a sink that dispatches stream data as commands through the Mediator.
        /// </summary>
        /// <typeparam name="TIn">The initial input type of the stream.</typeparam>
        /// <typeparam name="TCurrent">The current type of data in the stream.</typeparam>
        /// <typeparam name="TCommand">The type of command to dispatch.</typeparam>
        /// <typeparam name="TResult">The type of result returned by the command handler.</typeparam>
        /// <param name="builder">The stream builder instance.</param>
        /// <param name="mediator">The mediator instance.</param>
        /// <param name="commandFactory">A factory function to create commands from stream data.</param>
        /// <param name="resultHandler">Optional handler for command results.</param>
        /// <param name="errorHandler">Optional handler for errors.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>A sink builder to complete the stream configuration.</returns>
        public static ISinkBuilder<TIn, TCurrent> SinkToCommand<TIn, TCurrent, TCommand, TResult>(
            this IStreamBuilder<TIn, TCurrent> builder,
            IMediator mediator,
            Func<TCurrent, TCommand> commandFactory,
            Action<TCurrent, TResult> resultHandler = null,
            Action<TCurrent, Exception> errorHandler = null,
            CancellationToken cancellationToken = default)
            where TCommand : ICommand<TResult>
        {
            var sinkOperator = new MediatorCommandSinkOperator<TCurrent, TCommand, TResult>(
                mediator,
                commandFactory,
                resultHandler,
                errorHandler,
                cancellationToken);

            return builder.Sink(sinkOperator);
        }

        /// <summary>
        /// Adds a sink that dispatches stream data as void commands through the Mediator.
        /// </summary>
        /// <typeparam name="TIn">The initial input type of the stream.</typeparam>
        /// <typeparam name="TCurrent">The current type of data in the stream.</typeparam>
        /// <typeparam name="TCommand">The type of void command to dispatch.</typeparam>
        /// <param name="builder">The stream builder instance.</param>
        /// <param name="mediator">The mediator instance.</param>
        /// <param name="commandFactory">A factory function to create commands from stream data.</param>
        /// <param name="completionHandler">Optional handler called after successful command execution.</param>
        /// <param name="errorHandler">Optional handler for errors.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>A sink builder to complete the stream configuration.</returns>
        public static ISinkBuilder<TIn, TCurrent> SinkToVoidCommand<TIn, TCurrent, TCommand>(
            this IStreamBuilder<TIn, TCurrent> builder,
            IMediator mediator,
            Func<TCurrent, TCommand> commandFactory,
            Action<TCurrent> completionHandler = null,
            Action<TCurrent, Exception> errorHandler = null,
            CancellationToken cancellationToken = default)
            where TCommand : ICommand
        {
            var sinkOperator = new MediatorVoidCommandSinkOperator<TCurrent, TCommand>(
                mediator,
                commandFactory,
                completionHandler,
                errorHandler,
                cancellationToken);

            return builder.Sink(sinkOperator);
        }

        #endregion

        #region Notification Sinks

        /// <summary>
        /// Adds a sink that publishes stream data as notifications through the Mediator.
        /// </summary>
        /// <typeparam name="TIn">The initial input type of the stream.</typeparam>
        /// <typeparam name="TCurrent">The current type of data in the stream.</typeparam>
        /// <typeparam name="TNotification">The type of notification to publish.</typeparam>
        /// <param name="builder">The stream builder instance.</param>
        /// <param name="mediator">The mediator instance.</param>
        /// <param name="notificationFactory">A factory function to create notifications from stream data.</param>
        /// <param name="completionHandler">Optional handler called after successful publishing.</param>
        /// <param name="errorHandler">Optional handler for errors.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>A sink builder to complete the stream configuration.</returns>
        public static ISinkBuilder<TIn, TCurrent> SinkToNotification<TIn, TCurrent, TNotification>(
            this IStreamBuilder<TIn, TCurrent> builder,
            IMediator mediator,
            Func<TCurrent, TNotification> notificationFactory,
            Action<TCurrent> completionHandler = null,
            Action<TCurrent, Exception> errorHandler = null,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            var sinkOperator = new MediatorNotificationSinkOperator<TCurrent, TNotification>(
                mediator,
                notificationFactory,
                completionHandler,
                errorHandler,
                cancellationToken);

            return builder.Sink(sinkOperator);
        }

        /// <summary>
        /// Adds a sink that directly publishes stream data as notifications when the current type implements INotification.
        /// </summary>
        /// <typeparam name="TIn">The initial input type of the stream.</typeparam>
        /// <typeparam name="TNotification">The current type of data in the stream (must implement INotification).</typeparam>
        /// <param name="builder">The stream builder instance.</param>
        /// <param name="mediator">The mediator instance.</param>
        /// <param name="completionHandler">Optional handler called after successful publishing.</param>
        /// <param name="errorHandler">Optional handler for errors.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>A sink builder to complete the stream configuration.</returns>
        public static ISinkBuilder<TIn, TNotification> PublishNotification<TIn, TNotification>(
            this IStreamBuilder<TIn, TNotification> builder,
            IMediator mediator,
            Action<TNotification> completionHandler = null,
            Action<TNotification, Exception> errorHandler = null,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            var sinkOperator = new MediatorDirectNotificationSinkOperator<TNotification>(
                mediator,
                completionHandler,
                errorHandler,
                cancellationToken);

            return builder.Sink(sinkOperator);
        }

        #endregion
    }
}
