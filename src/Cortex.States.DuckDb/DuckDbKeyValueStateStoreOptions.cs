using System;

namespace Cortex.States.DuckDb
{
    /// <summary>
    /// Configuration options for the DuckDB key-value state store.
    /// </summary>
    public class DuckDbKeyValueStateStoreOptions
    {
        /// <summary>
        /// Gets or sets the path to the DuckDB database file.
        /// Use ":memory:" for an in-memory database.
        /// </summary>
        /// <example>
        /// "./data/mystore.duckdb" for a file-based database
        /// ":memory:" for an in-memory database
        /// </example>
        public string DatabasePath { get; set; }

        /// <summary>
        /// Gets or sets the name of the table to use for storing key-value pairs.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use an in-memory database.
        /// When true, the DatabasePath is ignored and ":memory:" is used.
        /// Default is false.
        /// </summary>
        public bool UseInMemory { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to create an index on the key column.
        /// Improves lookup performance for large datasets.
        /// Default is true.
        /// </summary>
        public bool CreateIndex { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of threads DuckDB should use.
        /// Set to 0 to use all available threads.
        /// Default is 0 (auto).
        /// </summary>
        public int Threads { get; set; } = 0;

        /// <summary>
        /// Gets or sets the maximum memory limit for DuckDB.
        /// Examples: "1GB", "512MB", "2GB"
        /// Leave null for default (80% of system memory).
        /// </summary>
        public string MaxMemory { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable object cache.
        /// Improves performance for repeated queries.
        /// Default is true.
        /// </summary>
        public bool EnableObjectCache { get; set; } = true;

        /// <summary>
        /// Gets or sets the access mode for the database.
        /// </summary>
        public DuckDbAccessMode AccessMode { get; set; } = DuckDbAccessMode.Automatic;

        /// <summary>
        /// Creates a new instance of DuckDbKeyValueStateStoreOptions for an in-memory database.
        /// </summary>
        /// <param name="tableName">The name of the table to use for storing key-value pairs.</param>
        /// <returns>A new options instance configured for in-memory use.</returns>
        public static DuckDbKeyValueStateStoreOptions InMemory(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            return new DuckDbKeyValueStateStoreOptions
            {
                DatabasePath = ":memory:",
                TableName = tableName,
                UseInMemory = true
            };
        }

        /// <summary>
        /// Creates a new instance of DuckDbKeyValueStateStoreOptions for a file-based database.
        /// </summary>
        /// <param name="databasePath">The path to the DuckDB database file.</param>
        /// <param name="tableName">The name of the table to use for storing key-value pairs.</param>
        /// <returns>A new options instance configured for file-based persistence.</returns>
        public static DuckDbKeyValueStateStoreOptions Persistent(string databasePath, string tableName)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentNullException(nameof(databasePath));
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            return new DuckDbKeyValueStateStoreOptions
            {
                DatabasePath = databasePath,
                TableName = tableName,
                UseInMemory = false
            };
        }

        /// <summary>
        /// Validates the options and throws if they are invalid.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(TableName))
                throw new ArgumentException("TableName is required", nameof(TableName));

            if (!UseInMemory && string.IsNullOrWhiteSpace(DatabasePath))
                throw new ArgumentException("DatabasePath is required when not using in-memory mode", nameof(DatabasePath));
        }
    }

    /// <summary>
    /// Specifies the access mode for the DuckDB database.
    /// </summary>
    public enum DuckDbAccessMode
    {
        /// <summary>
        /// DuckDB automatically determines the access mode.
        /// </summary>
        Automatic = 0,

        /// <summary>
        /// Opens the database in read-write mode.
        /// </summary>
        ReadWrite = 1,

        /// <summary>
        /// Opens the database in read-only mode.
        /// </summary>
        ReadOnly = 2
    }
}
