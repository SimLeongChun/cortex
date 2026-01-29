using System;

namespace Cortex.States.DuckDb
{
    /// <summary>
    /// Extension methods for creating and configuring DuckDB state stores.
    /// </summary>
    public static class DuckDbStateStoreExtensions
    {
        /// <summary>
        /// Creates a new DuckDB key-value state store with a persistent database.
        /// </summary>
        /// <typeparam name="TKey">The type of keys in the store.</typeparam>
        /// <typeparam name="TValue">The type of values in the store.</typeparam>
        /// <param name="name">A friendly name for the store.</param>
        /// <param name="databasePath">The path to the DuckDB database file.</param>
        /// <param name="tableName">The name of the table to use for storing key-value pairs.</param>
        /// <returns>A new DuckDB key-value state store instance.</returns>
        public static DuckDbKeyValueStateStore<TKey, TValue> CreateDuckDbStore<TKey, TValue>(
            string name,
            string databasePath,
            string tableName)
        {
            return new DuckDbKeyValueStateStore<TKey, TValue>(name, databasePath, tableName);
        }

        /// <summary>
        /// Creates a new DuckDB key-value state store with configuration options.
        /// </summary>
        /// <typeparam name="TKey">The type of keys in the store.</typeparam>
        /// <typeparam name="TValue">The type of values in the store.</typeparam>
        /// <param name="name">A friendly name for the store.</param>
        /// <param name="configureOptions">An action to configure the options.</param>
        /// <returns>A new DuckDB key-value state store instance.</returns>
        public static DuckDbKeyValueStateStore<TKey, TValue> CreateDuckDbStore<TKey, TValue>(
            string name,
            Action<DuckDbKeyValueStateStoreOptions> configureOptions)
        {
            var options = new DuckDbKeyValueStateStoreOptions();
            configureOptions(options);
            options.Validate();

            return new DuckDbKeyValueStateStore<TKey, TValue>(name, options);
        }

        /// <summary>
        /// Creates a new in-memory DuckDB key-value state store.
        /// </summary>
        /// <typeparam name="TKey">The type of keys in the store.</typeparam>
        /// <typeparam name="TValue">The type of values in the store.</typeparam>
        /// <param name="name">A friendly name for the store.</param>
        /// <param name="tableName">The name of the table to use for storing key-value pairs.</param>
        /// <returns>A new in-memory DuckDB key-value state store instance.</returns>
        public static DuckDbKeyValueStateStore<TKey, TValue> CreateInMemoryDuckDbStore<TKey, TValue>(
            string name,
            string tableName)
        {
            var options = DuckDbKeyValueStateStoreOptions.InMemory(tableName);
            return new DuckDbKeyValueStateStore<TKey, TValue>(name, options);
        }

        /// <summary>
        /// Creates a new persistent DuckDB key-value state store.
        /// </summary>
        /// <typeparam name="TKey">The type of keys in the store.</typeparam>
        /// <typeparam name="TValue">The type of values in the store.</typeparam>
        /// <param name="name">A friendly name for the store.</param>
        /// <param name="databasePath">The path to the DuckDB database file.</param>
        /// <param name="tableName">The name of the table to use for storing key-value pairs.</param>
        /// <returns>A new persistent DuckDB key-value state store instance.</returns>
        public static DuckDbKeyValueStateStore<TKey, TValue> CreatePersistentDuckDbStore<TKey, TValue>(
            string name,
            string databasePath,
            string tableName)
        {
            var options = DuckDbKeyValueStateStoreOptions.Persistent(databasePath, tableName);
            return new DuckDbKeyValueStateStore<TKey, TValue>(name, options);
        }
    }

    /// <summary>
    /// Builder class for creating DuckDB key-value state stores with fluent configuration.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the store.</typeparam>
    /// <typeparam name="TValue">The type of values in the store.</typeparam>
    public class DuckDbKeyValueStateStoreBuilder<TKey, TValue>
    {
        private string _name;
        private readonly DuckDbKeyValueStateStoreOptions _options = new DuckDbKeyValueStateStoreOptions();
        private Func<TKey, string> _keySerializer;
        private Func<TValue, string> _valueSerializer;
        private Func<string, TKey> _keyDeserializer;
        private Func<string, TValue> _valueDeserializer;

        /// <summary>
        /// Creates a new builder instance.
        /// </summary>
        /// <param name="name">The name of the state store.</param>
        public DuckDbKeyValueStateStoreBuilder(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Creates a new builder for a DuckDB key-value state store.
        /// </summary>
        /// <param name="name">The name of the state store.</param>
        /// <returns>A new builder instance.</returns>
        public static DuckDbKeyValueStateStoreBuilder<TKey, TValue> Create(string name)
        {
            return new DuckDbKeyValueStateStoreBuilder<TKey, TValue>(name);
        }

        /// <summary>
        /// Configures the store to use an in-memory database.
        /// </summary>
        /// <returns>The builder instance for chaining.</returns>
        public DuckDbKeyValueStateStoreBuilder<TKey, TValue> UseInMemory()
        {
            _options.UseInMemory = true;
            _options.DatabasePath = ":memory:";
            return this;
        }

        /// <summary>
        /// Configures the store to use a persistent database at the specified path.
        /// </summary>
        /// <param name="databasePath">The path to the DuckDB database file.</param>
        /// <returns>The builder instance for chaining.</returns>
        public DuckDbKeyValueStateStoreBuilder<TKey, TValue> WithDatabasePath(string databasePath)
        {
            _options.DatabasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
            _options.UseInMemory = false;
            return this;
        }

        /// <summary>
        /// Configures the table name to use for storing key-value pairs.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>The builder instance for chaining.</returns>
        public DuckDbKeyValueStateStoreBuilder<TKey, TValue> WithTableName(string tableName)
        {
            _options.TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
            return this;
        }

        /// <summary>
        /// Configures whether to create an index on the key column.
        /// </summary>
        /// <param name="createIndex">true to create an index; otherwise, false.</param>
        /// <returns>The builder instance for chaining.</returns>
        public DuckDbKeyValueStateStoreBuilder<TKey, TValue> WithIndex(bool createIndex = true)
        {
            _options.CreateIndex = createIndex;
            return this;
        }

        /// <summary>
        /// Configures the maximum memory limit for DuckDB.
        /// </summary>
        /// <param name="maxMemory">The maximum memory limit (e.g., "1GB", "512MB").</param>
        /// <returns>The builder instance for chaining.</returns>
        public DuckDbKeyValueStateStoreBuilder<TKey, TValue> WithMaxMemory(string maxMemory)
        {
            _options.MaxMemory = maxMemory;
            return this;
        }

        /// <summary>
        /// Configures the number of threads DuckDB should use.
        /// </summary>
        /// <param name="threads">The number of threads. Use 0 for auto-detect.</param>
        /// <returns>The builder instance for chaining.</returns>
        public DuckDbKeyValueStateStoreBuilder<TKey, TValue> WithThreads(int threads)
        {
            _options.Threads = threads;
            return this;
        }

        /// <summary>
        /// Configures the access mode for the database.
        /// </summary>
        /// <param name="accessMode">The access mode.</param>
        /// <returns>The builder instance for chaining.</returns>
        public DuckDbKeyValueStateStoreBuilder<TKey, TValue> WithAccessMode(DuckDbAccessMode accessMode)
        {
            _options.AccessMode = accessMode;
            return this;
        }

        /// <summary>
        /// Configures a custom key serializer.
        /// </summary>
        /// <param name="serializer">The key serializer function.</param>
        /// <returns>The builder instance for chaining.</returns>
        public DuckDbKeyValueStateStoreBuilder<TKey, TValue> WithKeySerializer(Func<TKey, string> serializer)
        {
            _keySerializer = serializer;
            return this;
        }

        /// <summary>
        /// Configures a custom value serializer.
        /// </summary>
        /// <param name="serializer">The value serializer function.</param>
        /// <returns>The builder instance for chaining.</returns>
        public DuckDbKeyValueStateStoreBuilder<TKey, TValue> WithValueSerializer(Func<TValue, string> serializer)
        {
            _valueSerializer = serializer;
            return this;
        }

        /// <summary>
        /// Configures a custom key deserializer.
        /// </summary>
        /// <param name="deserializer">The key deserializer function.</param>
        /// <returns>The builder instance for chaining.</returns>
        public DuckDbKeyValueStateStoreBuilder<TKey, TValue> WithKeyDeserializer(Func<string, TKey> deserializer)
        {
            _keyDeserializer = deserializer;
            return this;
        }

        /// <summary>
        /// Configures a custom value deserializer.
        /// </summary>
        /// <param name="deserializer">The value deserializer function.</param>
        /// <returns>The builder instance for chaining.</returns>
        public DuckDbKeyValueStateStoreBuilder<TKey, TValue> WithValueDeserializer(Func<string, TValue> deserializer)
        {
            _valueDeserializer = deserializer;
            return this;
        }

        /// <summary>
        /// Builds and returns the configured DuckDB key-value state store.
        /// </summary>
        /// <returns>A new DuckDB key-value state store instance.</returns>
        public DuckDbKeyValueStateStore<TKey, TValue> Build()
        {
            _options.Validate();

            return new DuckDbKeyValueStateStore<TKey, TValue>(
                _name,
                _options,
                _keySerializer,
                _valueSerializer,
                _keyDeserializer,
                _valueDeserializer);
        }
    }
}
