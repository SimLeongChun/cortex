using Cortex.Mediator.Notifications;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Processors
{
    /// <summary>
    /// Pipeline behavior that executes all registered pre-processors before the notification handler.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification being handled.</typeparam>
    public sealed class NotificationPreProcessorBehavior<TNotification> : INotificationPipelineBehavior<TNotification>
        where TNotification : INotification
    {
        private readonly IEnumerable<IRequestPreProcessor<TNotification>> _preProcessors;

        public NotificationPreProcessorBehavior(IEnumerable<IRequestPreProcessor<TNotification>> preProcessors)
        {
            _preProcessors = preProcessors;
        }

        public async Task Handle(
            TNotification notification,
            NotificationHandlerDelegate next,
            CancellationToken cancellationToken)
        {
            foreach (var processor in _preProcessors)
            {
                await processor.ProcessAsync(notification, cancellationToken);
            }

            await next();
        }
    }

    /// <summary>
    /// Pipeline behavior that executes all registered post-processors after the notification handler.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification being handled.</typeparam>
    public sealed class NotificationPostProcessorBehavior<TNotification> : INotificationPipelineBehavior<TNotification>
        where TNotification : INotification
    {
        private readonly IEnumerable<IRequestPostProcessor<TNotification>> _postProcessors;

        public NotificationPostProcessorBehavior(IEnumerable<IRequestPostProcessor<TNotification>> postProcessors)
        {
            _postProcessors = postProcessors;
        }

        public async Task Handle(
            TNotification notification,
            NotificationHandlerDelegate next,
            CancellationToken cancellationToken)
        {
            await next();

            foreach (var processor in _postProcessors)
            {
                await processor.ProcessAsync(notification, cancellationToken);
            }
        }
    }
}
