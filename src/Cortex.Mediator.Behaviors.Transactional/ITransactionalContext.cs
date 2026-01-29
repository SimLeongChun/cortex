using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Behaviors.Transactional
{
    /// <summary>
    /// Defines a contract for custom transaction management.
    /// Implement this interface to provide custom transaction handling (e.g., Entity Framework, Dapper, etc.).
    /// </summary>
    public interface ITransactionalContext
    {
        /// <summary>
        /// Begins a new transaction asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Commits the current transaction asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls back the current transaction asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
