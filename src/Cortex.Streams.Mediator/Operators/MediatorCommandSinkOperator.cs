using Cortex.Mediator;
using Cortex.Mediator.Commands;
using Cortex.Mediator.Notifications;
using Cortex.Streams.Operators;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.Mediator.Operators
{
    /// <summary>
    /// A sink operator that dispatches stream data as commands through the Mediator.
    /// This enables stream processing pipelines to integrate with CQRS command handlers.
    /// </summary>
    /// <typeparam name="TInput">The type of data received from the stream.</typeparam>
    /// <typeparam name="TCommand">The type of command to dispatch.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the command handler.</typeparam>
    public class MediatorCommandSinkOperator<TInput, TCommand, TResult> : ISinkOperator<TInput>
        where TCommand : ICommand<TResult>
    {
        private readonly IMediator _mediator;
        private readonly Func<TInput, TCommand> _commandFactory;
        private readonly Action<TInput, TResult> _resultHandler;
        private readonly Action<TInput, Exception> _errorHandler;
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediatorCommandSinkOperator{TInput, TCommand, TResult}"/> class.
        /// </summary>
        /// <param name="mediator">The mediator instance to dispatch commands through.</param>
        /// <param name="commandFactory">A factory function to create commands from stream data.</param>
        /// <param name="resultHandler">Optional handler for command results.</param>
        /// <param name="errorHandler">Optional handler for errors during command execution.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public MediatorCommandSinkOperator(
            IMediator mediator,
            Func<TInput, TCommand> commandFactory,
            Action<TInput, TResult> resultHandler = null,
            Action<TInput, Exception> errorHandler = null,
            CancellationToken cancellationToken = default)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
            _resultHandler = resultHandler;
            _errorHandler = errorHandler;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Starts the sink operator.
        /// </summary>
        public void Start()
        {
            // No initialization required
        }

        /// <summary>
        /// Processes the input data by dispatching it as a command through the mediator.
        /// </summary>
        /// <param name="input">The stream data to process.</param>
        public void Process(TInput input)
        {
            try
            {
                var command = _commandFactory(input);
                var task = _mediator.SendCommandAsync<TCommand, TResult>(command, _cancellationToken);
                
                // Wait for the task to complete synchronously for stream processing
                var result = task.GetAwaiter().GetResult();
                
                _resultHandler?.Invoke(input, result);
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                {
                    _errorHandler(input, ex);
                }
                else
                {
                    throw;
                }
            }
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
    /// A sink operator that dispatches stream data as void commands through the Mediator.
    /// Use this for commands that do not return a value.
    /// </summary>
    /// <typeparam name="TInput">The type of data received from the stream.</typeparam>
    /// <typeparam name="TCommand">The type of command to dispatch.</typeparam>
    public class MediatorVoidCommandSinkOperator<TInput, TCommand> : ISinkOperator<TInput>
        where TCommand : ICommand
    {
        private readonly IMediator _mediator;
        private readonly Func<TInput, TCommand> _commandFactory;
        private readonly Action<TInput> _completionHandler;
        private readonly Action<TInput, Exception> _errorHandler;
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediatorVoidCommandSinkOperator{TInput, TCommand}"/> class.
        /// </summary>
        /// <param name="mediator">The mediator instance to dispatch commands through.</param>
        /// <param name="commandFactory">A factory function to create commands from stream data.</param>
        /// <param name="completionHandler">Optional handler called after successful command execution.</param>
        /// <param name="errorHandler">Optional handler for errors during command execution.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public MediatorVoidCommandSinkOperator(
            IMediator mediator,
            Func<TInput, TCommand> commandFactory,
            Action<TInput> completionHandler = null,
            Action<TInput, Exception> errorHandler = null,
            CancellationToken cancellationToken = default)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
            _completionHandler = completionHandler;
            _errorHandler = errorHandler;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Starts the sink operator.
        /// </summary>
        public void Start()
        {
            // No initialization required
        }

        /// <summary>
        /// Processes the input data by dispatching it as a command through the mediator.
        /// </summary>
        /// <param name="input">The stream data to process.</param>
        public void Process(TInput input)
        {
            try
            {
                var command = _commandFactory(input);
                _mediator.SendCommandAsync<TCommand>(command, _cancellationToken).GetAwaiter().GetResult();
                _completionHandler?.Invoke(input);
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                {
                    _errorHandler(input, ex);
                }
                else
                {
                    throw;
                }
            }
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
