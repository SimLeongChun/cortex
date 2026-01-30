using Cortex.Mediator;
using Cortex.Mediator.Notifications;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using System;
using System.Threading;

namespace Cortex.Streams.Mediator.Operators
{
    /// <summary>
    /// A sink operator that publishes stream data as notifications through the Mediator.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of data received from the stream.</typeparam>
    /// <typeparam name="TNotification">The type of notification to publish.</typeparam>
    public class MediatorNotificationSinkOperator<TInput, TNotification> : ISinkOperator<TInput>, IErrorHandlingEnabled
        where TNotification : INotification
    {
        private static readonly string OperatorName = $"MediatorNotificationSinkOperator<{typeof(TInput).Name}, {typeof(TNotification).Name}>";

        private readonly IMediator _mediator;
        private readonly Func<TInput, TNotification> _notificationFactory;
        private readonly Action<TInput> _completionHandler;
        private readonly CancellationToken _cancellationToken;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediatorNotificationSinkOperator{TInput, TNotification}"/> class.
        /// </summary>
        /// <param name="mediator">The mediator instance to publish notifications through.</param>
        /// <param name="notificationFactory">A factory function to create notifications from stream data.</param>
        /// <param name="completionHandler">Optional handler called after successful notification publishing.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public MediatorNotificationSinkOperator(
            IMediator mediator,
            Func<TInput, TNotification> notificationFactory,
            Action<TInput> completionHandler = null,
            CancellationToken cancellationToken = default)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _notificationFactory = notificationFactory ?? throw new ArgumentNullException(nameof(notificationFactory));
            _completionHandler = completionHandler;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Sets the stream-level error handling options.
        /// </summary>
        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;
        }

        /// <summary>
        /// Starts the sink operator.
        /// </summary>
        public void Start()
        {
            // No initialization required
        }

        /// <summary>
        /// Processes the input data by publishing it as a notification through the mediator.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The stream data to process.</param>
        public void Process(TInput input)
        {
            ErrorHandlingHelper.TryExecute(
                _executionOptions,
                OperatorName,
                input,
                (Action<TInput>)PublishNotification);
        }

        private void PublishNotification(TInput input)
        {
            var notification = _notificationFactory(input);
            _mediator.PublishAsync(notification, _cancellationToken).GetAwaiter().GetResult();
            _completionHandler?.Invoke(input);
        }

        /// <summary>
        /// Stops the sink operator.
        /// </summary>
        public void Stop()
        {
            // No cleanup required
        }
    }

    /// <summary>
    /// A sink operator that directly publishes stream data as notifications when TInput implements INotification.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification (must implement INotification).</typeparam>
    public class MediatorDirectNotificationSinkOperator<TNotification> : ISinkOperator<TNotification>, IErrorHandlingEnabled
        where TNotification : INotification
    {
        private static readonly string OperatorName = $"MediatorDirectNotificationSinkOperator<{typeof(TNotification).Name}>";

        private readonly IMediator _mediator;
        private readonly Action<TNotification> _completionHandler;
        private readonly CancellationToken _cancellationToken;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediatorDirectNotificationSinkOperator{TNotification}"/> class.
        /// </summary>
        /// <param name="mediator">The mediator instance to publish notifications through.</param>
        /// <param name="completionHandler">Optional handler called after successful notification publishing.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public MediatorDirectNotificationSinkOperator(
            IMediator mediator,
            Action<TNotification> completionHandler = null,
            CancellationToken cancellationToken = default)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _completionHandler = completionHandler;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Sets the stream-level error handling options.
        /// </summary>
        public void SetErrorHandling(StreamExecutionOptions options)
        {
            _executionOptions = options ?? StreamExecutionOptions.Default;
        }

        /// <summary>
        /// Starts the sink operator.
        /// </summary>
        public void Start()
        {
            // No initialization required
        }

        /// <summary>
        /// Processes the notification by publishing it through the mediator.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="notification">The notification to publish.</param>
        public void Process(TNotification notification)
        {
            ErrorHandlingHelper.TryExecute(
                _executionOptions,
                OperatorName,
                notification,
                (Action<TNotification>)PublishNotification);
        }

        private void PublishNotification(TNotification notification)
        {
            _mediator.PublishAsync(notification, _cancellationToken).GetAwaiter().GetResult();
            _completionHandler?.Invoke(notification);
        }

        /// <summary>
        /// Stops the sink operator.
        /// </summary>
        public void Stop()
        {
            // No cleanup required
        }
    }
}
