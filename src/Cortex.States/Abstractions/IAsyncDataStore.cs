using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.States.Abstractions
{
    public interface IAsyncDataStore<TKey, TValue> : IDataStore<TKey, TValue>
    {
        Task<TValue> GetAsync(
        TKey key,
        CancellationToken cancellationToken = default);

        Task PutAsync(
            TKey key,
            TValue value,
            CancellationToken cancellationToken = default);

        Task<bool> ContainsKeyAsync(
            TKey key,
            CancellationToken cancellationToken = default);

        Task RemoveAsync(
            TKey key,
            CancellationToken cancellationToken = default);

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        IAsyncEnumerable<KeyValuePair<TKey, TValue>> GetAllAsync(
            CancellationToken cancellationToken = default);

        IAsyncEnumerable<TKey> GetKeysAsync(
            CancellationToken cancellationToken = default);
#endif

        Task<IDictionary<TKey, TValue>> GetManyAsync(
            IEnumerable<TKey> keys,
            CancellationToken cancellationToken = default);

        Task PutManyAsync(
            IEnumerable<KeyValuePair<TKey, TValue>> items,
            CancellationToken cancellationToken = default);
    }
}
