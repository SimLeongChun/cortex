using Cortex.Mediator;
using Cortex.Mediator.Streaming;
using Cortex.Streams.Operators;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.Mediator.Operators
{
    /// <summary>
    /// A source operator that consumes data from a Mediator streaming query and emits it into the stream.
    /// This enables using Mediator's streaming queries as data sources for stream processing pipelines.
    /// </summary>
    /// <typeparam name="TQuery">The type of streaming query to execute.</typeparam>
    /// <typeparam name="TOutput">The type of data emitted by the source.</typeparam>
    public class MediatorStreamQuerySourceOperator<TQuery, TOutput> : ISourceOperator<TOutput>
        where TQuery : IStreamQuery<TOutput>
    {
        private readonly IMediator _mediator;
        private readonly TQuery _query;
        private readonly Action<Exception> _errorHandler;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning;
        private Action<TOutput> _emit;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediatorStreamQuerySourceOperator{TQuery, TOutput}"/> class.
        /// </summary>
        /// <param name="mediator">The mediator instance to execute the streaming query through.</param>
        /// <param name="query">The streaming query to execute.</param>
        /// <param name="errorHandler">Optional handler for errors during query execution.</param>
        public MediatorStreamQuerySourceOperator(
            IMediator mediator,
            TQuery query,
            Action<Exception> errorHandler = null)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _errorHandler = errorHandler;
        }

        /// <summary>
        /// Starts the source operator, consuming data from the streaming query and emitting it to the stream.
        /// </summary>
        /// <param name="emit">An action to receive emitted data.</param>
        public void Start(Action<TOutput> emit)
        {
            _emit = emit ?? throw new ArgumentNullException(nameof(emit));
            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;

            // Start consuming the stream asynchronously
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var stream = _mediator.CreateStream<TQuery, TOutput>(_query, _cancellationTokenSource.Token);
                    
                    await foreach (var item in stream.WithCancellation(_cancellationTokenSource.Token))
                    {
                        if (!_isRunning)
                            break;
                        
                        _emit(item);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                }
                catch (Exception ex)
                {
                    if (_errorHandler != null)
                    {
                        _errorHandler(ex);
                    }
                    else
                    {
                        throw;
                    }
                }
            });
        }

        /// <summary>
        /// Stops the source operator.
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// A source operator that uses a factory function to create streaming queries dynamically.
    /// </summary>
    /// <typeparam name="TQuery">The type of streaming query to execute.</typeparam>
    /// <typeparam name="TOutput">The type of data emitted by the source.</typeparam>
    public class MediatorStreamQueryFactorySourceOperator<TQuery, TOutput> : ISourceOperator<TOutput>
        where TQuery : IStreamQuery<TOutput>
    {
        private readonly IMediator _mediator;
        private readonly Func<TQuery> _queryFactory;
        private readonly Action<Exception> _errorHandler;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning;
        private Action<TOutput> _emit;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediatorStreamQueryFactorySourceOperator{TQuery, TOutput}"/> class.
        /// </summary>
        /// <param name="mediator">The mediator instance to execute the streaming query through.</param>
        /// <param name="queryFactory">A factory function to create the streaming query.</param>
        /// <param name="errorHandler">Optional handler for errors during query execution.</param>
        public MediatorStreamQueryFactorySourceOperator(
            IMediator mediator,
            Func<TQuery> queryFactory,
            Action<Exception> errorHandler = null)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _queryFactory = queryFactory ?? throw new ArgumentNullException(nameof(queryFactory));
            _errorHandler = errorHandler;
        }

        /// <summary>
        /// Starts the source operator, consuming data from the streaming query and emitting it to the stream.
        /// </summary>
        /// <param name="emit">An action to receive emitted data.</param>
        public void Start(Action<TOutput> emit)
        {
            _emit = emit ?? throw new ArgumentNullException(nameof(emit));
            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var query = _queryFactory();
                    var stream = _mediator.CreateStream<TQuery, TOutput>(query, _cancellationTokenSource.Token);
                    
                    await foreach (var item in stream.WithCancellation(_cancellationTokenSource.Token))
                    {
                        if (!_isRunning)
                            break;
                        
                        _emit(item);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                }
                catch (Exception ex)
                {
                    if (_errorHandler != null)
                    {
                        _errorHandler(ex);
                    }
                    else
                    {
                        throw;
                    }
                }
            });
        }

        /// <summary>
        /// Stops the source operator.
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }
}
