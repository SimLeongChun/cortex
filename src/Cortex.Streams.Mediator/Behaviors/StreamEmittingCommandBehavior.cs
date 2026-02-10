using Cortex.Mediator.Commands;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Streams.Mediator.Behaviors
{
    /// <summary>
    /// A command pipeline behavior that emits commands to a stream before or after handler execution.
    /// This enables stream-based auditing, logging, or event sourcing of commands.
    /// </summary>
    /// <typeparam name="TCommand">The type of command.</typeparam>
    /// <typeparam name="TResult">The type of result.</typeparam>
    public class StreamEmittingCommandBehavior<TCommand, TResult> : ICommandPipelineBehavior<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        private readonly IStream<CommandExecutionEvent<TCommand, TResult>> _stream;
        private readonly bool _emitBeforeExecution;
        private readonly bool _emitAfterExecution;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamEmittingCommandBehavior{TCommand, TResult}"/> class.
        /// </summary>
        /// <param name="stream">The stream to emit command execution events to.</param>
        /// <param name="emitBeforeExecution">If true, emit an event before command execution.</param>
        /// <param name="emitAfterExecution">If true, emit an event after command execution.</param>
        public StreamEmittingCommandBehavior(
            IStream<CommandExecutionEvent<TCommand, TResult>> stream,
            bool emitBeforeExecution = false,
            bool emitAfterExecution = true)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _emitBeforeExecution = emitBeforeExecution;
            _emitAfterExecution = emitAfterExecution;
        }

        /// <summary>
        /// Handles the command in the pipeline, emitting events as configured.
        /// </summary>
        public async Task<TResult> Handle(
            TCommand command, 
            CommandHandlerDelegate<TResult> next, 
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            Exception exception = null;
            TResult result = default;

            try
            {
                if (_emitBeforeExecution)
                {
                    await _stream.EmitAsync(new CommandExecutionEvent<TCommand, TResult>
                    {
                        Command = command,
                        EventType = CommandExecutionEventType.BeforeExecution,
                        Timestamp = startTime
                    }, cancellationToken);
                }

                result = await next();
                return result;
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                if (_emitAfterExecution)
                {
                    var endTime = DateTime.UtcNow;
                    await _stream.EmitAsync(new CommandExecutionEvent<TCommand, TResult>
                    {
                        Command = command,
                        Result = result,
                        EventType = exception != null ? CommandExecutionEventType.Failed : CommandExecutionEventType.Succeeded,
                        Timestamp = endTime,
                        Duration = endTime - startTime,
                        Exception = exception
                    }, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Represents an event that occurs during command execution.
    /// </summary>
    /// <typeparam name="TCommand">The type of command.</typeparam>
    /// <typeparam name="TResult">The type of result.</typeparam>
    public class CommandExecutionEvent<TCommand, TResult>
    {
        /// <summary>
        /// Gets or sets the command being executed.
        /// </summary>
        public TCommand Command { get; set; }

        /// <summary>
        /// Gets or sets the result of the command execution.
        /// </summary>
        public TResult Result { get; set; }

        /// <summary>
        /// Gets or sets the type of execution event.
        /// </summary>
        public CommandExecutionEventType EventType { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the event.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the duration of command execution (for after events).
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Gets or sets the exception if the command failed.
        /// </summary>
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// The type of command execution event.
    /// </summary>
    public enum CommandExecutionEventType
    {
        /// <summary>
        /// Event emitted before command execution.
        /// </summary>
        BeforeExecution,

        /// <summary>
        /// Event emitted after successful command execution.
        /// </summary>
        Succeeded,

        /// <summary>
        /// Event emitted after failed command execution.
        /// </summary>
        Failed
    }
}
