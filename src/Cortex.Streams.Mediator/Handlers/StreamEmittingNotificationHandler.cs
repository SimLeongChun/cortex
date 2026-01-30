using Cortex.Mediator.Notifications;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.Mediator.Handlers
{
    /// <summary>
    /// A notification handler that emits notifications to a Cortex Stream.
    /// This enables routing Mediator notifications into stream processing pipelines.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to handle.</typeparam>
    public class StreamEmittingNotificationHandler<TNotification> : INotificationHandler<TNotification>
        where TNotification : INotification
    {
        private readonly IStream<TNotification, TNotification> _stream;
        private readonly Action<TNotification, Exception> _errorHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamEmittingNotificationHandler{TNotification}"/> class.
        /// </summary>
        /// <param name="stream">The stream to emit notifications to.</param>
        /// <param name="errorHandler">Optional handler for errors during emission.</param>
        public StreamEmittingNotificationHandler(
            IStream<TNotification, TNotification> stream,
            Action<TNotification, Exception> errorHandler = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _errorHandler = errorHandler;
        }

        /// <summary>
        /// Handles the notification by emitting it to the stream.
        /// </summary>
        /// <param name="notification">The notification to handle.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task Handle(TNotification notification, CancellationToken cancellationToken)
        {
            try
            {
                await _stream.EmitAsync(notification, cancellationToken);
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                {
                    _errorHandler(notification, ex);
                }
                else
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// A notification handler that transforms notifications before emitting to a stream.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to handle.</typeparam>
    /// <typeparam name="TStreamInput">The type of data expected by the stream.</typeparam>
    public class TransformingStreamNotificationHandler<TNotification, TStreamInput> : INotificationHandler<TNotification>
        where TNotification : INotification
    {
        private readonly IStream<TStreamInput, TStreamInput> _stream;
        private readonly Func<TNotification, TStreamInput> _transformer;
        private readonly Action<TNotification, Exception> _errorHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransformingStreamNotificationHandler{TNotification, TStreamInput}"/> class.
        /// </summary>
        /// <param name="stream">The stream to emit data to.</param>
        /// <param name="transformer">A function to transform notifications into stream input.</param>
        /// <param name="errorHandler">Optional handler for errors during emission.</param>
        public TransformingStreamNotificationHandler(
            IStream<TStreamInput, TStreamInput> stream,
            Func<TNotification, TStreamInput> transformer,
            Action<TNotification, Exception> errorHandler = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
            _errorHandler = errorHandler;
        }

        /// <summary>
        /// Handles the notification by transforming and emitting it to the stream.
        /// </summary>
        /// <param name="notification">The notification to handle.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task Handle(TNotification notification, CancellationToken cancellationToken)
        {
            try
            {
                var streamInput = _transformer(notification);
                await _stream.EmitAsync(streamInput, cancellationToken);
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                {
                    _errorHandler(notification, ex);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
