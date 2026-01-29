using System;
using System.Collections.Generic;
using System.Transactions;

namespace Cortex.Mediator.Behaviors.Transactional
{
    /// <summary>
    /// Configuration options for the transactional behavior.
    /// </summary>
    public class TransactionalOptions
    {
        /// <summary>
        /// Gets or sets the isolation level for the transaction.
        /// Default is <see cref="System.Transactions.IsolationLevel.ReadCommitted"/>.
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

        /// <summary>
        /// Gets or sets the timeout period for the transaction.
        /// Default is 30 seconds.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the transaction scope option.
        /// Default is <see cref="TransactionScopeOption.Required"/>.
        /// </summary>
        public TransactionScopeOption ScopeOption { get; set; } = TransactionScopeOption.Required;

        /// <summary>
        /// Gets or sets the async flow option for TransactionScope.
        /// Default is <see cref="TransactionScopeAsyncFlowOption.Enabled"/>.
        /// </summary>
        public TransactionScopeAsyncFlowOption AsyncFlowOption { get; set; } = TransactionScopeAsyncFlowOption.Enabled;

        /// <summary>
        /// Gets or sets a collection of command types that should be excluded from transactional behavior.
        /// Commands in this list will execute without a transaction wrapper.
        /// </summary>
        public HashSet<Type> ExcludedCommandTypes { get; set; } = new HashSet<Type>();

        /// <summary>
        /// Excludes a specific command type from transactional behavior.
        /// </summary>
        /// <typeparam name="TCommand">The type of command to exclude.</typeparam>
        /// <returns>The current options instance for fluent configuration.</returns>
        public TransactionalOptions ExcludeCommand<TCommand>()
        {
            ExcludedCommandTypes.Add(typeof(TCommand));
            return this;
        }

        /// <summary>
        /// Excludes multiple command types from transactional behavior.
        /// </summary>
        /// <param name="commandTypes">The command types to exclude.</param>
        /// <returns>The current options instance for fluent configuration.</returns>
        public TransactionalOptions ExcludeCommands(params Type[] commandTypes)
        {
            foreach (var type in commandTypes)
            {
                ExcludedCommandTypes.Add(type);
            }
            return this;
        }

        /// <summary>
        /// Creates a default instance of <see cref="TransactionalOptions"/>.
        /// </summary>
        /// <returns>A new instance with default settings.</returns>
        public static TransactionalOptions Default => new TransactionalOptions();
    }
}
