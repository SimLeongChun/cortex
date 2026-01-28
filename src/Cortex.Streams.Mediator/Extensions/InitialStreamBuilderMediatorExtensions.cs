using Cortex.Mediator;
using Cortex.Mediator.Streaming;
using Cortex.Streams.Abstractions;
using Cortex.Streams.Mediator.Operators;
using System;

namespace Cortex.Streams.Mediator.Extensions
{
    /// <summary>
    /// Extension methods for using Mediator streaming queries as stream sources.
    /// </summary>
    public static class InitialStreamBuilderMediatorExtensions
    {
        /// <summary>
        /// Starts a stream using a Mediator streaming query as the source.
        /// </summary>
        /// <typeparam name="TIn">The initial input type of the stream.</typeparam>
        /// <typeparam name="TQuery">The type of streaming query.</typeparam>
        /// <param name="builder">The initial stream builder instance.</param>
        /// <param name="mediator">The mediator instance.</param>
        /// <param name="query">The streaming query to execute.</param>
        /// <param name="errorHandler">Optional handler for errors during query execution.</param>
        /// <returns>A stream builder for further configuration.</returns>
        public static IStreamBuilder<TIn, TIn> StreamFromQuery<TIn, TQuery>(
            this IInitialStreamBuilder<TIn> builder,
            IMediator mediator,
            TQuery query,
            Action<Exception> errorHandler = null)
            where TQuery : IStreamQuery<TIn>
        {
            var sourceOperator = new MediatorStreamQuerySourceOperator<TQuery, TIn>(
                mediator,
                query,
                errorHandler);

            return builder.Stream(sourceOperator);
        }

        /// <summary>
        /// Starts a stream using a factory function to create the Mediator streaming query.
        /// This is useful when the query needs to be created lazily or with current context.
        /// </summary>
        /// <typeparam name="TIn">The initial input type of the stream.</typeparam>
        /// <typeparam name="TQuery">The type of streaming query.</typeparam>
        /// <param name="builder">The initial stream builder instance.</param>
        /// <param name="mediator">The mediator instance.</param>
        /// <param name="queryFactory">A factory function to create the streaming query.</param>
        /// <param name="errorHandler">Optional handler for errors during query execution.</param>
        /// <returns>A stream builder for further configuration.</returns>
        public static IStreamBuilder<TIn, TIn> StreamFromQueryFactory<TIn, TQuery>(
            this IInitialStreamBuilder<TIn> builder,
            IMediator mediator,
            Func<TQuery> queryFactory,
            Action<Exception> errorHandler = null)
            where TQuery : IStreamQuery<TIn>
        {
            var sourceOperator = new MediatorStreamQueryFactorySourceOperator<TQuery, TIn>(
                mediator,
                queryFactory,
                errorHandler);

            return builder.Stream(sourceOperator);
        }
    }
}
