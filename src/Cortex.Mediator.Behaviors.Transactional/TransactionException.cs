using System;

namespace Cortex.Mediator.Behaviors.Transactional
{
    /// <summary>
    /// Exception thrown when a transaction fails to commit or roll back.
    /// </summary>
    public class TransactionException : Exception
    {
        /// <summary>
        /// Gets the type of transaction failure.
        /// </summary>
        public TransactionFailureType FailureType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="failureType">The type of transaction failure.</param>
        public TransactionException(string message, TransactionFailureType failureType) 
            : base(message)
        {
            FailureType = failureType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="failureType">The type of transaction failure.</param>
        /// <param name="innerException">The inner exception that caused this exception.</param>
        public TransactionException(string message, TransactionFailureType failureType, Exception innerException) 
            : base(message, innerException)
        {
            FailureType = failureType;
        }
    }

    /// <summary>
    /// Represents the type of transaction failure.
    /// </summary>
    public enum TransactionFailureType
    {
        /// <summary>
        /// The transaction failed to begin.
        /// </summary>
        BeginFailed,

        /// <summary>
        /// The transaction failed to commit.
        /// </summary>
        CommitFailed,

        /// <summary>
        /// The transaction failed to roll back.
        /// </summary>
        RollbackFailed,

        /// <summary>
        /// The transaction timed out.
        /// </summary>
        Timeout,

        /// <summary>
        /// An unknown transaction error occurred.
        /// </summary>
        Unknown
    }
}
