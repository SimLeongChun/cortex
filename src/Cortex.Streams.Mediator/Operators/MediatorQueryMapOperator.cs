using Cortex.Mediator;
using Cortex.Mediator.Queries;
using Cortex.Streams.Operators;
using System;
using System.Threading;

namespace Cortex.Streams.Mediator.Operators
{
    /// <summary>
    /// A map operator that transforms stream data by executing a query through the Mediator.
    /// This enables enriching stream data using CQRS query handlers.
    /// </summary>
    /// <typeparam name="TInput">The type of data received from the stream.</typeparam>
    /// <typeparam name="TQuery">The type of query to execute.</typeparam>
    /// <typeparam name="TOutput">The type of data produced after query execution.</typeparam>
    public class MediatorQueryMapOperator<TInput, TQuery, TOutput> : IOperator
        where TQuery : IQuery<TOutput>
    {
        private readonly IMediator _mediator;
        private readonly Func<TInput, TQuery> _queryFactory;
        private readonly Func<TInput, TOutput, TOutput> _resultProjector;
        private readonly Action<TInput, Exception> _errorHandler;
        private readonly CancellationToken _cancellationToken;
        private IOperator _nextOperator;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediatorQueryMapOperator{TInput, TQuery, TOutput}"/> class.
        /// </summary>
        /// <param name="mediator">The mediator instance to execute queries through.</param>
        /// <param name="queryFactory">A factory function to create queries from stream data.</param>
        /// <param name="resultProjector">Optional function to project the query result. If null, the query result is passed through.</param>
        /// <param name="errorHandler">Optional handler for errors during query execution.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public MediatorQueryMapOperator(
            IMediator mediator,
            Func<TInput, TQuery> queryFactory,
            Func<TInput, TOutput, TOutput> resultProjector = null,
            Action<TInput, Exception> errorHandler = null,
            CancellationToken cancellationToken = default)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _queryFactory = queryFactory ?? throw new ArgumentNullException(nameof(queryFactory));
            _resultProjector = resultProjector;
            _errorHandler = errorHandler;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Processes the input data by executing a query and passing the result downstream.
        /// </summary>
        /// <param name="input">The data to process.</param>
        public void Process(object input)
        {
            try
            {
                var typedInput = (TInput)input;
                var query = _queryFactory(typedInput);
                var result = _mediator.SendQueryAsync<TQuery, TOutput>(query, _cancellationToken).GetAwaiter().GetResult();
                
                var output = _resultProjector != null ? _resultProjector(typedInput, result) : result;
                
                _nextOperator?.Process(output);
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                {
                    _errorHandler((TInput)input, ex);
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Sets the next operator in the pipeline.
        /// </summary>
        /// <param name="nextOperator">The next operator.</param>
        public void SetNext(IOperator nextOperator)
        {
            _nextOperator = nextOperator;
        }
    }

    /// <summary>
    /// A map operator that enriches stream data by combining it with query results.
    /// </summary>
    /// <typeparam name="TInput">The type of data received from the stream.</typeparam>
    /// <typeparam name="TQuery">The type of query to execute.</typeparam>
    /// <typeparam name="TQueryResult">The type of result returned by the query.</typeparam>
    /// <typeparam name="TOutput">The type of enriched output data.</typeparam>
    public class MediatorQueryEnrichOperator<TInput, TQuery, TQueryResult, TOutput> : IOperator
        where TQuery : IQuery<TQueryResult>
    {
        private readonly IMediator _mediator;
        private readonly Func<TInput, TQuery> _queryFactory;
        private readonly Func<TInput, TQueryResult, TOutput> _enricher;
        private readonly Action<TInput, Exception> _errorHandler;
        private readonly TOutput _defaultOutput;
        private readonly bool _skipOnError;
        private readonly CancellationToken _cancellationToken;
        private IOperator _nextOperator;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediatorQueryEnrichOperator{TInput, TQuery, TQueryResult, TOutput}"/> class.
        /// </summary>
        /// <param name="mediator">The mediator instance to execute queries through.</param>
        /// <param name="queryFactory">A factory function to create queries from stream data.</param>
        /// <param name="enricher">A function to combine input data with query results.</param>
        /// <param name="errorHandler">Optional handler for errors during query execution.</param>
        /// <param name="defaultOutput">Default output to use when an error occurs and skipOnError is false.</param>
        /// <param name="skipOnError">If true, skip the item on error instead of using defaultOutput.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public MediatorQueryEnrichOperator(
            IMediator mediator,
            Func<TInput, TQuery> queryFactory,
            Func<TInput, TQueryResult, TOutput> enricher,
            Action<TInput, Exception> errorHandler = null,
            TOutput defaultOutput = default,
            bool skipOnError = false,
            CancellationToken cancellationToken = default)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _queryFactory = queryFactory ?? throw new ArgumentNullException(nameof(queryFactory));
            _enricher = enricher ?? throw new ArgumentNullException(nameof(enricher));
            _errorHandler = errorHandler;
            _defaultOutput = defaultOutput;
            _skipOnError = skipOnError;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Processes the input data by executing a query and enriching the input with the result.
        /// </summary>
        /// <param name="input">The data to process.</param>
        public void Process(object input)
        {
            var typedInput = (TInput)input;
            
            try
            {
                var query = _queryFactory(typedInput);
                var result = _mediator.SendQueryAsync<TQuery, TQueryResult>(query, _cancellationToken).GetAwaiter().GetResult();
                var output = _enricher(typedInput, result);
                
                _nextOperator?.Process(output);
            }
            catch (Exception ex)
            {
                _errorHandler?.Invoke(typedInput, ex);
                
                if (!_skipOnError)
                {
                    _nextOperator?.Process(_defaultOutput);
                }
            }
        }

        /// <summary>
        /// Sets the next operator in the pipeline.
        /// </summary>
        /// <param name="nextOperator">The next operator.</param>
        public void SetNext(IOperator nextOperator)
        {
            _nextOperator = nextOperator;
        }
    }
}
