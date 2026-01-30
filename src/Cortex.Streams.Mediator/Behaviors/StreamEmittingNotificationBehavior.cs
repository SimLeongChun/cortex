using Cortex.Mediator.Notifications;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.Mediator.Behaviors
{
    /// <summary>
    /// Delegate for the next notification handler in the pipeline.
    /// </summary>
    public delegate Task NotificationHandlerDelegate();

    /// <summary>
    /// A notification pipeline behavior that emits notifications to a stream.
    /// This enables stream-based auditing or event streaming of notifications.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification.</typeparam>
    public class StreamEmittingNotificationBehavior<TNotification> : INotificationPipelineBehavior<TNotification>
        where TNotification : INotification
    {
        private readonly IStream<NotificationEvent<TNotification>, NotificationEvent<TNotification>> _stream;
        private readonly bool _emitBeforeHandling;
        private readonly bool _emitAfterHandling;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamEmittingNotificationBehavior{TNotification}"/> class.
        /// </summary>
        /// <param name="stream">The stream to emit notification events to.</param>
        /// <param name="emitBeforeHandling">If true, emit an event before notification handling.</param>
        /// <param name="emitAfterHandling">If true, emit an event after notification handling.</param>
        public StreamEmittingNotificationBehavior(
            IStream<NotificationEvent<TNotification>, NotificationEvent<TNotification>> stream,
            bool emitBeforeHandling = false,
            bool emitAfterHandling = true)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _emitBeforeHandling = emitBeforeHandling;
            _emitAfterHandling = emitAfterHandling;
        }

        /// <summary>
        /// Handles the notification in the pipeline, emitting events as configured.
        /// </summary>
        public async Task Handle(
            TNotification notification,
            Cortex.Mediator.Notifications.NotificationHandlerDelegate next,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            Exception exception = null;

            try
            {
                if (_emitBeforeHandling)
                {
                    await _stream.EmitAsync(new NotificationEvent<TNotification>
                    {
                        Notification = notification,
                        EventType = NotificationEventType.BeforeHandling,
                        Timestamp = startTime
                    }, cancellationToken);
                }

                await next();
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                if (_emitAfterHandling)
                {
                    var endTime = DateTime.UtcNow;
                    await _stream.EmitAsync(new NotificationEvent<TNotification>
                    {
                        Notification = notification,
                        EventType = exception != null ? NotificationEventType.Failed : NotificationEventType.Handled,
                        Timestamp = endTime,
                        Duration = endTime - startTime,
                        Exception = exception
                    }, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Represents an event that occurs during notification handling.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification.</typeparam>
    public class NotificationEvent<TNotification>
    {
        /// <summary>
        /// Gets or sets the notification being handled.
        /// </summary>
        public TNotification Notification { get; set; }

        /// <summary>
        /// Gets or sets the type of notification event.
        /// </summary>
        public NotificationEventType EventType { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the event.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the duration of notification handling (for after events).
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Gets or sets the exception if handling failed.
        /// </summary>
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// The type of notification event.
    /// </summary>
    public enum NotificationEventType
    {
        /// <summary>
        /// Event emitted before notification handling.
        /// </summary>
        BeforeHandling,

        /// <summary>
        /// Event emitted after successful notification handling.
        /// </summary>
        Handled,

        /// <summary>
        /// Event emitted after failed notification handling.
        /// </summary>
        Failed
    }
}
