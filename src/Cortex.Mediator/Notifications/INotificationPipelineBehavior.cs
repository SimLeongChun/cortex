using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Notifications
{
    /// <summary>
    /// Defines a pipeline behavior for wrapping notification handlers.
    /// This allows cross-cutting concerns like logging, error handling, and validation
    /// to be applied around notification handling.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification being handled.</typeparam>
    public interface INotificationPipelineBehavior<in TNotification>
        where TNotification : INotification
    {
        /// <summary>
        /// Handles the notification and invokes the next behavior in the pipeline.
        /// </summary>
        /// <param name="notification">The notification being handled.</param>
        /// <param name="next">The delegate to invoke the next behavior or the final handler.</param>
        /// <param name="cancellationToken">A cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Handle(
            TNotification notification,
            NotificationHandlerDelegate next,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Represents a delegate that wraps the notification handler execution.
    /// </summary>
    public delegate Task NotificationHandlerDelegate();
}
