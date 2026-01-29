# Cortex.States.DuckDb

[![NuGet Version](https://img.shields.io/nuget/v/Cortex.States.DuckDb?label=Cortex.States.DuckDb)](https://www.nuget.org/packages/Cortex.States.DuckDb)

**Cortex.States.DuckDb** is a state store implementation for the Cortex Data Framework that uses [DuckDB](https://duckdb.org/) as the underlying storage engine. DuckDB is an in-process analytical database management system designed for fast analytical queries, making it an excellent choice for scenarios requiring both transactional state management and analytical capabilities.

## Features

- **High-Performance Analytics**: Leverages DuckDB's columnar storage and vectorized query execution
- **In-Memory & Persistent Storage**: Supports both in-memory databases for fast processing and file-based persistence
- **Native Export Capabilities**: Export data directly to Parquet or CSV formats
- **Batch Operations**: Efficient bulk insert and delete operations with transaction support
- **Thread-Safe**: Built-in thread safety for concurrent access
- **Flexible Serialization**: Customizable key and value serialization
- **Fluent Builder API**: Easy configuration through builder pattern

## Installation

### Using the .NET CLI

```bash
dotnet add package Cortex.States.DuckDb
```

### Using the Package Manager Console

```powershell
Install-Package Cortex.States.DuckDb
```

## Quick Start

### Basic Usage

```csharp
using Cortex.States.DuckDb;

// Create a persistent DuckDB state store
var stateStore = new DuckDbKeyValueStateStore<string, int>(
    name: "MyStateStore",
    databasePath: "./data/mystore.duckdb",
    tableName: "KeyValueStore"
);

// Store values
stateStore.Put("counter", 42);
stateStore.Put("total", 100);

// Retrieve values
var counter = stateStore.Get("counter"); // Returns 42

// Check if key exists
if (stateStore.ContainsKey("counter"))
{
    Console.WriteLine("Counter exists!");
}

// Remove a value
stateStore.Remove("counter");

// Get all keys
foreach (var key in stateStore.GetKeys())
{
    Console.WriteLine($"Key: {key}");
}

// Don't forget to dispose
stateStore.Dispose();
```

### Using the Fluent Builder

```csharp
using Cortex.States.DuckDb;

// Create store using fluent builder
var stateStore = DuckDbKeyValueStateStoreBuilder<string, OrderSummary>
    .Create("OrderStore")
    .WithDatabasePath("./data/orders.duckdb")
    .WithTableName("Orders")
    .WithIndex(true)
    .WithMaxMemory("2GB")
    .WithThreads(4)
    .Build();

// Use the store
stateStore.Put("ORD-001", new OrderSummary { Total = 99.99m, Status = "Completed" });
```

### In-Memory Database

```csharp
using Cortex.States.DuckDb;

// Create an in-memory store for fast processing
var inMemoryStore = DuckDbKeyValueStateStoreBuilder<string, decimal>
    .Create("TemporaryStore")
    .UseInMemory()
    .WithTableName("TempData")
    .Build();

// Perfect for temporary computations
inMemoryStore.Put("sum", 1234.56m);
```

### Using with Options

```csharp
using Cortex.States.DuckDb;

// Create options for fine-grained control
var options = new DuckDbKeyValueStateStoreOptions
{
    DatabasePath = "./data/analytics.duckdb",
    TableName = "AnalyticsState",
    CreateIndex = true,
    MaxMemory = "4GB",
    Threads = 8,
    AccessMode = DuckDbAccessMode.ReadWrite
};

var stateStore = new DuckDbKeyValueStateStore<string, AnalyticsData>(
    name: "AnalyticsStore",
    options: options
);
```

### Factory Methods

```csharp
using Cortex.States.DuckDb;

// Quick creation methods
var persistentStore = DuckDbStateStoreExtensions
    .CreatePersistentDuckDbStore<string, Product>("ProductStore", "./data/products.duckdb", "Products");

var inMemoryStore = DuckDbStateStoreExtensions
    .CreateInMemoryDuckDbStore<string, Session>("SessionStore", "Sessions");
```

## Advanced Features

### Batch Operations

```csharp
// Efficient bulk insert
var items = new List<KeyValuePair<string, decimal>>
{
    new("price-1", 10.99m),
    new("price-2", 20.99m),
    new("price-3", 30.99m)
};

stateStore.PutMany(items);

// Bulk delete
stateStore.RemoveMany(new[] { "price-1", "price-2" });
```

### Export to Parquet/CSV

DuckDB has native support for Parquet and CSV formats, making data export seamless:

```csharp
// Export to Parquet (ideal for analytics)
stateStore.ExportToParquet("./exports/state-backup.parquet");

// Export to CSV (ideal for data sharing)
stateStore.ExportToCsv("./exports/state-backup.csv");
```

### Count and Clear

```csharp
// Get total count
var count = stateStore.Count();
Console.WriteLine($"Total items: {count}");

// Clear all items
stateStore.Clear();
```

### Checkpoint

For persistent databases, you can force a checkpoint to ensure all data is written to disk:

```csharp
stateStore.Checkpoint();
```

## Integration with Cortex Streams

Use DuckDB state store with Cortex Streams for stateful stream processing:

```csharp
using Cortex.Streams;
using Cortex.States.DuckDb;

// Create the state store
var stateStore = new DuckDbKeyValueStateStore<string, int>(
    name: "WordCountStore",
    databasePath: "./data/wordcount.duckdb",
    tableName: "WordCounts"
);

// Use in a stream pipeline
var stream = StreamBuilder<string>.CreateNewStream("WordCountStream")
    .Stream()
    .FlatMap(line => line.Split(' '))
    .GroupBy(word => word)
    .Aggregate(
        stateStore,
        (count, word) => count + 1,
        initialValue: 0)
    .Sink(result => Console.WriteLine($"{result.Key}: {result.Value}"))
    .Build();

stream.Start();
```

## Custom Serialization

You can provide custom serializers for complex types:

```csharp
using System.Text.Json;

var stateStore = new DuckDbKeyValueStateStore<Guid, ComplexObject>(
    name: "ComplexStore",
    databasePath: "./data/complex.duckdb",
    tableName: "ComplexData",
    keySerializer: key => key.ToString(),
    keyDeserializer: str => Guid.Parse(str),
    valueSerializer: value => JsonSerializer.Serialize(value),
    valueDeserializer: str => JsonSerializer.Deserialize<ComplexObject>(str)!
);
```

## Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `DatabasePath` | Path to the DuckDB database file. Use `:memory:` for in-memory | Required |
| `TableName` | Name of the table for key-value storage | Required |
| `UseInMemory` | Use in-memory database instead of file | `false` |
| `CreateIndex` | Create index on key column for faster lookups | `true` |
| `MaxMemory` | Maximum memory limit (e.g., "1GB", "512MB") | Auto |
| `Threads` | Number of threads (0 = auto) | `0` |
| `AccessMode` | Database access mode (Automatic, ReadWrite, ReadOnly) | `Automatic` |

## When to Use DuckDB State Store

DuckDB is particularly well-suited for:

- **Analytical workloads**: When you need to run analytical queries on your state
- **Large datasets**: Efficient columnar storage for large amounts of data
- **Data export requirements**: Native Parquet/CSV export capabilities
- **Embedded analytics**: In-process database without external dependencies
- **Temporary processing**: Fast in-memory mode for intermediate computations

Consider other state stores when:

- You need distributed state across multiple nodes (use Cassandra, MongoDB)
- You require extreme write throughput (use RocksDB)
- You need full ACID transactions across multiple operations (use PostgreSQL, SQL Server)

## Thread Safety

The `DuckDbKeyValueStateStore` is thread-safe and can be used concurrently from multiple threads. For in-memory databases, a persistent connection is maintained to ensure data consistency.

## Error Handling

```csharp
try
{
    var value = stateStore.Get("non-existent-key");
    if (value == null)
    {
        Console.WriteLine("Key not found");
    }
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Store not initialized: {ex.Message}");
}
```

## Best Practices

1. **Dispose properly**: Always dispose of the state store when done to release resources
2. **Use batch operations**: For bulk inserts/deletes, use `PutMany` and `RemoveMany`
3. **Choose appropriate storage**: Use in-memory for temporary data, file-based for persistence
4. **Set memory limits**: Configure `MaxMemory` for large datasets to prevent excessive memory usage
5. **Regular checkpoints**: Call `Checkpoint()` periodically for critical data in persistent mode

## Requirements

- .NET 7.0 or later
- DuckDB.NET.Data package (automatically included)

## License

MIT License - see the [license file](../src/Cortex.States.DuckDb/Assets/license.md) for details.

## Related Packages

- [Cortex.States](https://www.nuget.org/packages/Cortex.States) - Core state management
- [Cortex.States.RocksDb](https://www.nuget.org/packages/Cortex.States.RocksDb) - RocksDB state store
- [Cortex.States.SQLite](https://www.nuget.org/packages/Cortex.States.SQLite) - SQLite state store
- [Cortex.Streams](https://www.nuget.org/packages/Cortex.Streams) - Core streaming capabilities
