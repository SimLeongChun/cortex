using Cortex.Mediator;
using Cortex.Mediator.Commands;
using Cortex.Streams.Operators;
using System;
using System.Threading;

namespace Cortex.Streams.Mediator.Operators
{
    /// <summary>
    /// A filter operator that uses command execution result to determine if data should pass through.
    /// This enables conditional stream processing based on CQRS command results.
    /// </summary>
    /// <typeparam name="TInput">The type of data in the stream.</typeparam>
    /// <typeparam name="TCommand">The type of command to execute.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the command.</typeparam>
    public class MediatorCommandFilterOperator<TInput, TCommand, TResult> : IOperator
        where TCommand : ICommand<TResult>
    {
        private readonly IMediator _mediator;
        private readonly Func<TInput, TCommand> _commandFactory;
        private readonly Func<TInput, TResult, bool> _filterPredicate;
        private readonly Action<TInput, Exception> _errorHandler;
        private readonly bool _passOnError;
        private readonly CancellationToken _cancellationToken;
        private IOperator _nextOperator;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediatorCommandFilterOperator{TInput, TCommand, TResult}"/> class.
        /// </summary>
        /// <param name="mediator">The mediator instance to execute commands through.</param>
        /// <param name="commandFactory">A factory function to create commands from stream data.</param>
        /// <param name="filterPredicate">A predicate that uses the command result to determine if data should pass through.</param>
        /// <param name="errorHandler">Optional handler for errors during command execution.</param>
        /// <param name="passOnError">If true, pass the item through on error; if false, filter it out.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public MediatorCommandFilterOperator(
            IMediator mediator,
            Func<TInput, TCommand> commandFactory,
            Func<TInput, TResult, bool> filterPredicate,
            Action<TInput, Exception> errorHandler = null,
            bool passOnError = false,
            CancellationToken cancellationToken = default)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
            _filterPredicate = filterPredicate ?? throw new ArgumentNullException(nameof(filterPredicate));
            _errorHandler = errorHandler;
            _passOnError = passOnError;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Processes the input data, executing a command and filtering based on the result.
        /// </summary>
        /// <param name="input">The data to process.</param>
        public void Process(object input)
        {
            var typedInput = (TInput)input;
            
            try
            {
                var command = _commandFactory(typedInput);
                var result = _mediator.SendCommandAsync<TCommand, TResult>(command, _cancellationToken).GetAwaiter().GetResult();
                
                if (_filterPredicate(typedInput, result))
                {
                    _nextOperator?.Process(typedInput);
                }
            }
            catch (Exception ex)
            {
                _errorHandler?.Invoke(typedInput, ex);
                
                if (_passOnError)
                {
                    _nextOperator?.Process(typedInput);
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
