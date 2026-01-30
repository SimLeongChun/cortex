using Cortex.Mediator.Queries;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Processors
{
    /// <summary>
    /// Pipeline behavior that executes all registered pre-processors before the query handler.
    /// </summary>
    /// <typeparam name="TQuery">The type of query being handled.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the query.</typeparam>
    public sealed class QueryPreProcessorBehavior<TQuery, TResult> : IQueryPipelineBehavior<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        private readonly IEnumerable<IRequestPreProcessor<TQuery>> _preProcessors;

        public QueryPreProcessorBehavior(IEnumerable<IRequestPreProcessor<TQuery>> preProcessors)
        {
            _preProcessors = preProcessors;
        }

        public async Task<TResult> Handle(
            TQuery query,
            QueryHandlerDelegate<TResult> next,
            CancellationToken cancellationToken)
        {
            foreach (var processor in _preProcessors)
            {
                await processor.ProcessAsync(query, cancellationToken);
            }

            return await next();
        }
    }

    /// <summary>
    /// Pipeline behavior that executes all registered post-processors after the query handler.
    /// </summary>
    /// <typeparam name="TQuery">The type of query being handled.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the query.</typeparam>
    public sealed class QueryPostProcessorBehavior<TQuery, TResult> : IQueryPipelineBehavior<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        private readonly IEnumerable<IRequestPostProcessor<TQuery, TResult>> _postProcessors;

        public QueryPostProcessorBehavior(IEnumerable<IRequestPostProcessor<TQuery, TResult>> postProcessors)
        {
            _postProcessors = postProcessors;
        }

        public async Task<TResult> Handle(
            TQuery query,
            QueryHandlerDelegate<TResult> next,
            CancellationToken cancellationToken)
        {
            var response = await next();

            foreach (var processor in _postProcessors)
            {
                await processor.ProcessAsync(query, response, cancellationToken);
            }

            return response;
        }
    }
}
