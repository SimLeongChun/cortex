using Cortex.Mediator;
using Cortex.Mediator.Commands;
using Cortex.Mediator.Notifications;
using Cortex.Streams.Mediator.Handlers;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Cortex.Streams.Mediator.DependencyInjection
{
    /// <summary>
    /// Extension methods for registering Cortex.Streams.Mediator services with dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a notification handler that emits notifications directly to a stream.
        /// </summary>
        /// <typeparam name="TNotification">The type of notification to handle.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="streamFactory">A factory function to create or resolve the stream.</param>
        /// <param name="errorHandler">Optional error handler for emission failures.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddStreamEmittingNotificationHandler<TNotification>(
            this IServiceCollection services,
            Func<IServiceProvider, IStream<TNotification>> streamFactory,
            Action<TNotification, Exception> errorHandler = null)
            where TNotification : INotification
        {
            services.AddTransient<INotificationHandler<TNotification>>(sp =>
            {
                var stream = streamFactory(sp);
                return new StreamEmittingNotificationHandler<TNotification>(stream, errorHandler);
            });

            return services;
        }

        /// <summary>
        /// Registers a notification handler that transforms and emits notifications to a stream.
        /// </summary>
        /// <typeparam name="TNotification">The type of notification to handle.</typeparam>
        /// <typeparam name="TStreamInput">The type of data expected by the stream.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="streamFactory">A factory function to create or resolve the stream.</param>
        /// <param name="transformer">A function to transform notifications into stream input.</param>
        /// <param name="errorHandler">Optional error handler for emission failures.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddTransformingStreamNotificationHandler<TNotification, TStreamInput>(
            this IServiceCollection services,
            Func<IServiceProvider, IStream<TStreamInput>> streamFactory,
            Func<TNotification, TStreamInput> transformer,
            Action<TNotification, Exception> errorHandler = null)
            where TNotification : INotification
        {
            services.AddTransient<INotificationHandler<TNotification>>(sp =>
            {
                var stream = streamFactory(sp);
                return new TransformingStreamNotificationHandler<TNotification, TStreamInput>(
                    stream, 
                    transformer, 
                    errorHandler);
            });

            return services;
        }

        /// <summary>
        /// Adds the Cortex Streams Mediator integration services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCortexStreamsMediatorIntegration(this IServiceCollection services)
        {
            // Register factory for creating mediator-integrated stream operators
            services.AddSingleton<IMediatorStreamFactory, MediatorStreamFactory>();
            
            return services;
        }
    }

    /// <summary>
    /// Factory interface for creating mediator-integrated stream components.
    /// </summary>
    public interface IMediatorStreamFactory
    {
        /// <summary>
        /// Creates a command sink operator.
        /// </summary>
        Operators.MediatorCommandSinkOperator<TInput, TCommand, TResult> CreateCommandSink<TInput, TCommand, TResult>(
            Func<TInput, TCommand> commandFactory,
            Action<TInput, TResult> resultHandler = null,
            System.Threading.CancellationToken cancellationToken = default)
            where TCommand : ICommand<TResult>;

        /// <summary>
        /// Creates a notification sink operator.
        /// </summary>
        Operators.MediatorNotificationSinkOperator<TInput, TNotification> CreateNotificationSink<TInput, TNotification>(
            Func<TInput, TNotification> notificationFactory,
            Action<TInput> completionHandler = null,
            System.Threading.CancellationToken cancellationToken = default)
            where TNotification : INotification;
    }

    /// <summary>
    /// Default implementation of the mediator stream factory.
    /// </summary>
    internal class MediatorStreamFactory : IMediatorStreamFactory
    {
        private readonly IMediator _mediator;

        public MediatorStreamFactory(IMediator mediator)
        {
            _mediator = mediator;
        }

        public Operators.MediatorCommandSinkOperator<TInput, TCommand, TResult> CreateCommandSink<TInput, TCommand, TResult>(
            Func<TInput, TCommand> commandFactory,
            Action<TInput, TResult> resultHandler = null,
            System.Threading.CancellationToken cancellationToken = default)
            where TCommand : ICommand<TResult>
        {
            return new Operators.MediatorCommandSinkOperator<TInput, TCommand, TResult>(
                _mediator,
                commandFactory,
                resultHandler,
                cancellationToken);
        }

        public Operators.MediatorNotificationSinkOperator<TInput, TNotification> CreateNotificationSink<TInput, TNotification>(
            Func<TInput, TNotification> notificationFactory,
            Action<TInput> completionHandler = null,
            System.Threading.CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return new Operators.MediatorNotificationSinkOperator<TInput, TNotification>(
                _mediator,
                notificationFactory,
                completionHandler,
                cancellationToken);
        }
    }
}
