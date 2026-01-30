using Cortex.Mediator;
using Cortex.Mediator.Commands;
using Cortex.Streams.ErrorHandling;
using Cortex.Streams.Operators;
using System;
using System.Threading;

namespace Cortex.Streams.Mediator.Operators
{
    /// <summary>
    /// A sink operator that dispatches stream data as commands through the Mediator.
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of data received from the stream.</typeparam>
    /// <typeparam name="TCommand">The type of command to dispatch.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the command handler.</typeparam>
    public class MediatorCommandSinkOperator<TInput, TCommand, TResult> : ISinkOperator<TInput>, IErrorHandlingEnabled
        where TCommand : ICommand<TResult>
    {
        private static readonly string OperatorName = $"MediatorCommandSinkOperator<{typeof(TInput).Name}, {typeof(TCommand).Name}, {typeof(TResult).Name}>";

        private readonly IMediator _mediator;
        private readonly Func<TInput, TCommand> _commandFactory;
        private readonly Action<TInput, TResult> _resultHandler;
        private readonly CancellationToken _cancellationToken;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediatorCommandSinkOperator{TInput, TCommand, TResult}"/> class.
        /// </summary>
        /// <param name="mediator">The mediator instance to dispatch commands through.</param>
        /// <param name="commandFactory">A factory function to create commands from stream data.</param>
        /// <param name="resultHandler">Optional handler for command results.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public MediatorCommandSinkOperator(
            IMediator mediator,
            Func<TInput, TCommand> commandFactory,
            Action<TInput, TResult> resultHandler = null,
            CancellationToken cancellationToken = default)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
            _resultHandler = resultHandler;
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
        /// Processes the input data by dispatching it as a command through the mediator.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The stream data to process.</param>
        public void Process(TInput input)
        {
            ErrorHandlingHelper.TryExecute(
                _executionOptions,
                OperatorName,
                input,
                (Action<TInput>)DispatchCommand);
        }

        private void DispatchCommand(TInput input)
        {
            var command = _commandFactory(input);
            var result = _mediator.SendCommandAsync<TCommand, TResult>(command, _cancellationToken).GetAwaiter().GetResult();
            _resultHandler?.Invoke(input, result);
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
    /// Implements IErrorHandlingEnabled to participate in stream-level error handling.
    /// </summary>
    /// <typeparam name="TInput">The type of data received from the stream.</typeparam>
    /// <typeparam name="TCommand">The type of command to dispatch.</typeparam>
    public class MediatorVoidCommandSinkOperator<TInput, TCommand> : ISinkOperator<TInput>, IErrorHandlingEnabled
        where TCommand : ICommand
    {
        private static readonly string OperatorName = $"MediatorVoidCommandSinkOperator<{typeof(TInput).Name}, {typeof(TCommand).Name}>";

        private readonly IMediator _mediator;
        private readonly Func<TInput, TCommand> _commandFactory;
        private readonly Action<TInput> _completionHandler;
        private readonly CancellationToken _cancellationToken;
        private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediatorVoidCommandSinkOperator{TInput, TCommand}"/> class.
        /// </summary>
        /// <param name="mediator">The mediator instance to dispatch commands through.</param>
        /// <param name="commandFactory">A factory function to create commands from stream data.</param>
        /// <param name="completionHandler">Optional handler called after successful command execution.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public MediatorVoidCommandSinkOperator(
            IMediator mediator,
            Func<TInput, TCommand> commandFactory,
            Action<TInput> completionHandler = null,
            CancellationToken cancellationToken = default)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
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
        /// Processes the input data by dispatching it as a command through the mediator.
        /// Uses stream-level error handling configured via IErrorHandlingEnabled.
        /// </summary>
        /// <param name="input">The stream data to process.</param>
        public void Process(TInput input)
        {
            ErrorHandlingHelper.TryExecute(
                _executionOptions,
                OperatorName,
                input,
                (Action<TInput>)DispatchCommand);
        }

        private void DispatchCommand(TInput input)
        {
            var command = _commandFactory(input);
            _mediator.SendCommandAsync<TCommand>(command, _cancellationToken).GetAwaiter().GetResult();
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
}
