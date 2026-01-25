using Cortex.Mediator.Commands;
using Cortex.Mediator.Queries;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator
{
    /// <summary>
    /// Extension methods for <see cref="IMediator"/> that provide simplified API 
    /// with automatic type inference for commands and queries.
    /// </summary>
    public static class MediatorExtensions
    {
        /// <summary>
        /// Sends a command and returns the result. The result type is inferred from the command.
        /// </summary>
        /// <typeparam name="TResult">The type of result returned by the command (inferred).</typeparam>
        /// <param name="mediator">The mediator instance.</param>
        /// <param name="command">The command to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the command execution.</returns>
        /// <example>
        /// <code>
        /// // Instead of:
        /// var result = await mediator.SendCommandAsync&lt;CreateUserCommand, Guid&gt;(command);
        /// 
        /// // You can now write:
        /// var result = await mediator.SendAsync(command);
        /// </code>
        /// </example>
        public static Task<TResult> SendAsync<TResult>(
            this IMediator mediator,
            ICommand<TResult> command,
            CancellationToken cancellationToken = default)
        {
            return mediator.SendCommandAsync(command, cancellationToken);
        }

        /// <summary>
        /// Sends a command that does not return a value.
        /// </summary>
        /// <param name="mediator">The mediator instance.</param>
        /// <param name="command">The command to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <example>
        /// <code>
        /// await mediator.SendAsync(new DeleteUserCommand { UserId = userId });
        /// </code>
        /// </example>
        public static Task SendAsync(
            this IMediator mediator,
            ICommand command,
            CancellationToken cancellationToken = default)
        {
            return mediator.SendCommandAsync(command, cancellationToken);
        }

        /// <summary>
        /// Sends a query and returns the result. The result type is inferred from the query.
        /// </summary>
        /// <typeparam name="TResult">The type of result returned by the query (inferred).</typeparam>
        /// <param name="mediator">The mediator instance.</param>
        /// <param name="query">The query to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the query execution.</returns>
        /// <example>
        /// <code>
        /// // Instead of:
        /// var user = await mediator.SendQueryAsync&lt;GetUserQuery, UserDto&gt;(query);
        /// 
        /// // You can now write:
        /// var user = await mediator.QueryAsync(query);
        /// </code>
        /// </example>
        public static Task<TResult> QueryAsync<TResult>(
            this IMediator mediator,
            IQuery<TResult> query,
            CancellationToken cancellationToken = default)
        {
            return mediator.SendQueryAsync(query, cancellationToken);
        }
    }
}
