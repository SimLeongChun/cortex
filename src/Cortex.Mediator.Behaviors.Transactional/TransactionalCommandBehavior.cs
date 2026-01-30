using Cortex.Mediator.Commands;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Cortex.Mediator.Behaviors.Transactional
{
    /// <summary>
    /// Pipeline behavior that wraps command execution within a transaction scope.
    /// Ensures atomic execution of commands with automatic commit on success and rollback on failure.
    /// </summary>
    /// <typeparam name="TCommand">The type of command being handled.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the command.</typeparam>
    public sealed class TransactionalCommandBehavior<TCommand, TResult> : ICommandPipelineBehavior<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        private readonly TransactionalOptions _options;
        private readonly ITransactionalContext _transactionalContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionalCommandBehavior{TCommand, TResult}"/> class.
        /// </summary>
        /// <param name="options">The transactional options for configuring transaction behavior.</param>
        /// <param name="transactionalContext">Optional custom transactional context. If null, default TransactionScope is used.</param>
        public TransactionalCommandBehavior(TransactionalOptions options, ITransactionalContext transactionalContext = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _transactionalContext = transactionalContext;
        }

        /// <summary>
        /// Handles the command execution within a transaction scope.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="next">The delegate to invoke the next behavior in the pipeline.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the command execution.</returns>
        public async Task<TResult> Handle(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            // Check if command should be excluded from transactional behavior
            if (ShouldExcludeCommand(command))
            {
                return await next();
            }

            // Use custom transactional context if provided
            if (_transactionalContext != null)
            {
                return await ExecuteWithCustomContext(next, cancellationToken);
            }

            // Use default TransactionScope
            return await ExecuteWithTransactionScope(next, cancellationToken);
        }

        private bool ShouldExcludeCommand(TCommand command)
        {
            var commandType = command.GetType();

            // Check for NonTransactionalAttribute
            if (Attribute.IsDefined(commandType, typeof(NonTransactionalAttribute)))
            {
                return true;
            }

            // Check if command type is in the exclusion list
            if (_options.ExcludedCommandTypes != null && _options.ExcludedCommandTypes.Contains(commandType))
            {
                return true;
            }

            return false;
        }

        private async Task<TResult> ExecuteWithCustomContext(CommandHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            await _transactionalContext.BeginTransactionAsync(cancellationToken);

            try
            {
                var result = await next();
                await _transactionalContext.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await _transactionalContext.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task<TResult> ExecuteWithTransactionScope(CommandHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            var transactionOptions = new TransactionOptions
            {
                IsolationLevel = _options.IsolationLevel,
                Timeout = _options.Timeout
            };

            using (var scope = new TransactionScope(
                _options.ScopeOption,
                transactionOptions,
                _options.AsyncFlowOption))
            {
                var result = await next();
                scope.Complete();
                return result;
            }
        }
    }
}
