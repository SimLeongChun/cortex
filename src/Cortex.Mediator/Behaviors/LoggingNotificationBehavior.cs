using Cortex.Mediator.Notifications;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Behaviors
{
    /// <summary>
    /// Pipeline behavior for logging notification execution.
    /// </summary>
    public sealed class LoggingNotificationBehavior<TNotification> : INotificationPipelineBehavior<TNotification>
        where TNotification : INotification
    {
        private readonly ILogger<LoggingNotificationBehavior<TNotification>> _logger;

        public LoggingNotificationBehavior(ILogger<LoggingNotificationBehavior<TNotification>> logger)
        {
            _logger = logger;
        }

        public async Task Handle(
            TNotification notification,
            NotificationHandlerDelegate next,
            CancellationToken cancellationToken)
        {
            var notificationName = typeof(TNotification).Name;
            _logger.LogInformation("Publishing notification {NotificationName}", notificationName);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await next();

                stopwatch.Stop();
                _logger.LogInformation(
                    "Notification {NotificationName} published successfully in {ElapsedMilliseconds} ms",
                    notificationName,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(
                    ex,
                    "Error publishing notification {NotificationName} after {ElapsedMilliseconds} ms",
                    notificationName,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
