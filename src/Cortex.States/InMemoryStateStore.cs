using Cortex.States.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.States
{
    public class InMemoryStateStore<TKey, TValue> : IAsyncDataStore<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> _store = new ConcurrentDictionary<TKey, TValue>();

        public string Name { get; }

        public InMemoryStateStore(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public TValue Get(TKey key)
        {
            if (_store.TryGetValue(key, out var value))
                return value;

            return default;
        }

        public void Put(TKey key, TValue value)
        {
            _store[key] = value;
        }

        public bool ContainsKey(TKey key)
        {
            return _store.ContainsKey(key);
        }

        public void Remove(TKey key)
        {
            _store.TryRemove(key, out _);
        }

        public IEnumerable<KeyValuePair<TKey, TValue>> GetAll()
        {
            return _store;
        }

        public IEnumerable<TKey> GetKeys()
        {
            return _store.Keys;
        }

        public Task<TValue> GetAsync(TKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _store.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task PutAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task<bool> ContainsKeyAsync(TKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var contains = _store.ContainsKey(key);
            return Task.FromResult(contains);
        }

        public Task RemoveAsync(TKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _store.TryRemove(key, out _);
            return Task.CompletedTask;
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        public async IAsyncEnumerable<KeyValuePair<TKey, TValue>> GetAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var item in _store)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;

                // Optional: yield control for very large stores
                await Task.Yield();
            }
        }

        public async IAsyncEnumerable<TKey> GetKeysAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var key in _store.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return key;

                // Optional: yield control for very large keysets
                await Task.Yield();
            }
        }
#endif

        public Task<IDictionary<TKey, TValue>> GetManyAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            cancellationToken.ThrowIfCancellationRequested();

            var result = new Dictionary<TKey, TValue>();

            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_store.TryGetValue(key, out var value))
                {
                    result[key] = value;
                }
            }

            return Task.FromResult((IDictionary<TKey, TValue>)result);
        }

        public Task PutManyAsync(IEnumerable<KeyValuePair<TKey, TValue>> items, CancellationToken cancellationToken = default)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _store[item.Key] = item.Value;
            }

            return Task.CompletedTask;
        }
    }
}
