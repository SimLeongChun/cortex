using DuckDB.NET.Data;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.States.DuckDb
{
    /// <summary>
    /// A key-value state store implementation backed by DuckDB.
    /// DuckDB is an in-process analytical database management system designed for fast analytics.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the store.</typeparam>
    /// <typeparam name="TValue">The type of values in the store.</typeparam>
    public class DuckDbKeyValueStateStore<TKey, TValue> : IDataStore<TKey, TValue>, IDisposable
    {
        private readonly string _connectionString;
        private readonly string _tableName;
        private readonly Func<TKey, string> _keySerializer;
        private readonly Func<TValue, string> _valueSerializer;
        private readonly Func<string, TKey> _keyDeserializer;
        private readonly Func<string, TValue> _valueDeserializer;
        private readonly DuckDbKeyValueStateStoreOptions _options;
        private readonly DuckDBConnection _persistentConnection;
        private readonly object _connectionLock = new object();

        private static readonly SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);
        private volatile bool _isInitialized;
        private bool _disposed;

        /// <summary>
        /// Gets the name of the state store.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Initializes a new instance of the DuckDbKeyValueStateStore.
        /// </summary>
        /// <param name="name">A friendly name for the store.</param>
        /// <param name="databasePath">
        ///     The file path to the DuckDB database.
        ///     Use ":memory:" for an in-memory database, or provide a file path for persistence.
        /// </param>
        /// <param name="tableName">The name of the table to use for storing state entries.</param>
        /// <param name="keySerializer">Optional key serializer. If not provided, JSON serialization is used.</param>
        /// <param name="valueSerializer">Optional value serializer. If not provided, JSON serialization is used.</param>
        /// <param name="keyDeserializer">Optional key deserializer. If not provided, JSON deserialization is used.</param>
        /// <param name="valueDeserializer">Optional value deserializer. If not provided, JSON deserialization is used.</param>
        public DuckDbKeyValueStateStore(
            string name,
            string databasePath,
            string tableName,
            Func<TKey, string> keySerializer = null,
            Func<TValue, string> valueSerializer = null,
            Func<string, TKey> keyDeserializer = null,
            Func<string, TValue> valueDeserializer = null)
            : this(name, new DuckDbKeyValueStateStoreOptions
            {
                DatabasePath = databasePath,
                TableName = tableName
            }, keySerializer, valueSerializer, keyDeserializer, valueDeserializer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DuckDbKeyValueStateStore with options.
        /// </summary>
        /// <param name="name">A friendly name for the store.</param>
        /// <param name="options">Configuration options for the DuckDB state store.</param>
        /// <param name="keySerializer">Optional key serializer. If not provided, JSON serialization is used.</param>
        /// <param name="valueSerializer">Optional value serializer. If not provided, JSON serialization is used.</param>
        /// <param name="keyDeserializer">Optional key deserializer. If not provided, JSON deserialization is used.</param>
        /// <param name="valueDeserializer">Optional value deserializer. If not provided, JSON deserialization is used.</param>
        public DuckDbKeyValueStateStore(
            string name,
            DuckDbKeyValueStateStoreOptions options,
            Func<TKey, string> keySerializer = null,
            Func<TValue, string> valueSerializer = null,
            Func<string, TKey> keyDeserializer = null,
            Func<string, TValue> valueDeserializer = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.DatabasePath))
                throw new ArgumentException("DatabasePath is required", nameof(options));
            if (string.IsNullOrWhiteSpace(options.TableName))
                throw new ArgumentException("TableName is required", nameof(options));

            Name = name;
            _options = options;
            _tableName = options.TableName;

            // Build connection string
            _connectionString = BuildConnectionString(options);

            // Assign custom or default (JSON-based) serializers/deserializers
            _keySerializer = keySerializer ?? (key => JsonSerializer.Serialize(key));
            _valueSerializer = valueSerializer ?? (value => JsonSerializer.Serialize(value));
            _keyDeserializer = keyDeserializer ?? (str => JsonSerializer.Deserialize<TKey>(str));
            _valueDeserializer = valueDeserializer ?? (str => JsonSerializer.Deserialize<TValue>(str));

            // Create a persistent connection for in-memory databases
            if (options.UseInMemory || options.DatabasePath == ":memory:")
            {
                _persistentConnection = new DuckDBConnection(_connectionString);
                _persistentConnection.Open();
            }

            // Initialize the table
            InitializeAsync().GetAwaiter().GetResult();
        }

        private static string BuildConnectionString(DuckDbKeyValueStateStoreOptions options)
        {
            if (options.UseInMemory || options.DatabasePath == ":memory:")
            {
                return "DataSource=:memory:";
            }

            return $"DataSource={options.DatabasePath}";
        }

        private DuckDBConnection GetConnection()
        {
            if (_persistentConnection != null)
            {
                return _persistentConnection;
            }

            var connection = new DuckDBConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private void ReleaseConnection(DuckDBConnection connection)
        {
            // Only close and dispose if it's not the persistent connection
            if (connection != _persistentConnection)
            {
                connection.Close();
                connection.Dispose();
            }
        }

        private async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _initializationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_isInitialized) return;

                var connection = GetConnection();
                try
                {
                    // Create the table if it does not exist
                    var createTableSql = $@"
                        CREATE TABLE IF NOT EXISTS ""{_tableName}"" (
                            key VARCHAR PRIMARY KEY,
                            value VARCHAR
                        );";

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = createTableSql;
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }

                    // Create index for faster lookups if configured
                    if (_options.CreateIndex)
                    {
                        var createIndexSql = $@"
                            CREATE INDEX IF NOT EXISTS idx_{_tableName}_key 
                            ON ""{_tableName}"" (key);";

                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = createIndexSql;
                            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    ReleaseConnection(connection);
                }

                _isInitialized = true;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("DuckDbKeyValueStateStore is not properly initialized.");
            }
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <returns>The value associated with the specified key, or default if the key is not found.</returns>
        public TValue Get(TKey key)
        {
            EnsureInitialized();

            var serializedKey = _keySerializer(key);
            var connection = GetConnection();

            try
            {
                var sql = $@"SELECT value FROM ""{_tableName}"" WHERE key = $key;";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.Add(new DuckDBParameter("key", serializedKey));

                    var result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                        return default;

                    return _valueDeserializer(result.ToString());
                }
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Adds or updates the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to add or update.</param>
        /// <param name="value">The value to add or update.</param>
        public void Put(TKey key, TValue value)
        {
            EnsureInitialized();

            var serializedKey = _keySerializer(key);
            var serializedValue = _valueSerializer(value);
            var connection = GetConnection();

            try
            {
                // DuckDB supports INSERT OR REPLACE syntax
                var sql = $@"
                    INSERT OR REPLACE INTO ""{_tableName}"" (key, value)
                    VALUES ($key, $value);";

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.Add(new DuckDBParameter("key", serializedKey));
                    cmd.Parameters.Add(new DuckDBParameter("value", serializedValue));
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Determines whether the store contains the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the store.</param>
        /// <returns>true if the store contains an element with the specified key; otherwise, false.</returns>
        public bool ContainsKey(TKey key)
        {
            EnsureInitialized();

            var serializedKey = _keySerializer(key);
            var connection = GetConnection();

            try
            {
                var sql = $@"SELECT COUNT(*) FROM ""{_tableName}"" WHERE key = $key;";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.Add(new DuckDBParameter("key", serializedKey));

                    var count = Convert.ToInt64(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Removes the value with the specified key from the store.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        public void Remove(TKey key)
        {
            EnsureInitialized();

            var serializedKey = _keySerializer(key);
            var connection = GetConnection();

            try
            {
                var sql = $@"DELETE FROM ""{_tableName}"" WHERE key = $key;";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.Add(new DuckDBParameter("key", serializedKey));
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Returns all key-value pairs in the store.
        /// </summary>
        /// <returns>An enumerable of all key-value pairs in the store.</returns>
        public IEnumerable<KeyValuePair<TKey, TValue>> GetAll()
        {
            EnsureInitialized();

            var results = new List<KeyValuePair<TKey, TValue>>();
            var connection = GetConnection();

            try
            {
                var sql = $@"SELECT key, value FROM ""{_tableName}"";";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var serializedKey = reader.GetString(0);
                            var serializedValue = reader.IsDBNull(1) ? null : reader.GetString(1);

                            var key = _keyDeserializer(serializedKey);
                            var value = serializedValue == null ? default : _valueDeserializer(serializedValue);

                            results.Add(new KeyValuePair<TKey, TValue>(key, value));
                        }
                    }
                }

                return results;
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Returns all keys in the store.
        /// </summary>
        /// <returns>An enumerable of all keys in the store.</returns>
        public IEnumerable<TKey> GetKeys()
        {
            EnsureInitialized();

            var results = new List<TKey>();
            var connection = GetConnection();

            try
            {
                var sql = $@"SELECT key FROM ""{_tableName}"";";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var serializedKey = reader.GetString(0);
                            results.Add(_keyDeserializer(serializedKey));
                        }
                    }
                }

                return results;
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Adds or updates multiple key-value pairs in a batch operation.
        /// </summary>
        /// <param name="items">The key-value pairs to add or update.</param>
        public void PutMany(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            EnsureInitialized();

            var connection = GetConnection();

            try
            {
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var item in items)
                        {
                            var serializedKey = _keySerializer(item.Key);
                            var serializedValue = _valueSerializer(item.Value);

                            var sql = $@"
                                INSERT OR REPLACE INTO ""{_tableName}"" (key, value)
                                VALUES ($key, $value);";

                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.CommandText = sql;
                                cmd.Transaction = transaction;
                                cmd.Parameters.Add(new DuckDBParameter("key", serializedKey));
                                cmd.Parameters.Add(new DuckDBParameter("value", serializedValue));
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Removes multiple keys from the store in a batch operation.
        /// </summary>
        /// <param name="keys">The keys to remove.</param>
        public void RemoveMany(IEnumerable<TKey> keys)
        {
            EnsureInitialized();

            var connection = GetConnection();

            try
            {
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var key in keys)
                        {
                            var serializedKey = _keySerializer(key);

                            var sql = $@"DELETE FROM ""{_tableName}"" WHERE key = $key;";
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.CommandText = sql;
                                cmd.Transaction = transaction;
                                cmd.Parameters.Add(new DuckDBParameter("key", serializedKey));
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Gets the count of items in the store.
        /// </summary>
        /// <returns>The number of items in the store.</returns>
        public long Count()
        {
            EnsureInitialized();

            var connection = GetConnection();

            try
            {
                var sql = $@"SELECT COUNT(*) FROM ""{_tableName}"";";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    return Convert.ToInt64(cmd.ExecuteScalar());
                }
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Clears all items from the store.
        /// </summary>
        public void Clear()
        {
            EnsureInitialized();

            var connection = GetConnection();

            try
            {
                var sql = $@"DELETE FROM ""{_tableName}"";";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Exports the state store data to a Parquet file.
        /// DuckDB has native support for Parquet format.
        /// </summary>
        /// <param name="filePath">The path to the Parquet file to create.</param>
        public void ExportToParquet(string filePath)
        {
            EnsureInitialized();

            var connection = GetConnection();

            try
            {
                var sql = $@"COPY ""{_tableName}"" TO '{filePath}' (FORMAT PARQUET);";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Exports the state store data to a CSV file.
        /// </summary>
        /// <param name="filePath">The path to the CSV file to create.</param>
        public void ExportToCsv(string filePath)
        {
            EnsureInitialized();

            var connection = GetConnection();

            try
            {
                var sql = $@"COPY ""{_tableName}"" TO '{filePath}' (FORMAT CSV, HEADER);";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Creates a checkpoint to ensure all data is written to disk.
        /// Only applicable for persistent databases.
        /// </summary>
        public void Checkpoint()
        {
            if (_options.UseInMemory || _options.DatabasePath == ":memory:")
            {
                return; // No checkpoint needed for in-memory databases
            }

            var connection = GetConnection();

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "CHECKPOINT;";
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                ReleaseConnection(connection);
            }
        }

        /// <summary>
        /// Releases all resources used by the DuckDbKeyValueStateStore.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the DuckDbKeyValueStateStore and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    // Create checkpoint before closing for persistent databases
                    if (!_options.UseInMemory && _options.DatabasePath != ":memory:")
                    {
                        Checkpoint();
                    }
                }
                catch
                {
                    // Ignore checkpoint errors during disposal
                }

                _persistentConnection?.Close();
                _persistentConnection?.Dispose();
                _initializationLock?.Dispose();
            }

            _disposed = true;
        }
    }
}
