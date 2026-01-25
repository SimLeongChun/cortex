using Cortex.Mediator.Queries;

namespace Cortex.Mediator.Caching
{
    /// <summary>
    /// Interface for generating cache keys for queries.
    /// Implement this interface to provide custom cache key generation logic.
    /// </summary>
    public interface ICacheKeyGenerator
    {
        /// <summary>
        /// Generates a cache key for the specified query.
        /// </summary>
        /// <typeparam name="TQuery">The type of the query.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="query">The query instance.</param>
        /// <returns>A unique cache key for the query.</returns>
        string GenerateKey<TQuery, TResult>(TQuery query) where TQuery : IQuery<TResult>;
    }
}
