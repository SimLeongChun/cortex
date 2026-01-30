# Stream Performance & Async Processing

This guide covers the performance optimization features in Cortex.Streams, including buffered async processing, backpressure handling, and high-throughput configurations.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Configuration Options](#configuration-options)
- [Backpressure Strategies](#backpressure-strategies)
- [Emit Methods](#emit-methods)
- [Monitoring & Statistics](#monitoring--statistics)
- [Preset Configurations](#preset-configurations)
- [Best Practices](#best-practices)
- [Migration Guide](#migration-guide)
- [API Reference](#api-reference)

---

## Overview

Cortex.Streams provides optional performance features for high-throughput scenarios:

| Feature | Description |
|---------|-------------|
| **Buffered Processing** | Internal bounded buffer with async consumers |
| **Backpressure Handling** | Configurable strategies when buffer is full |
| **Async Emission** | Non-blocking `EmitAsync` and `EmitAndForget` |
| **Batch Processing** | Process multiple items in batches for throughput |
| **Parallel Consumers** | Multiple concurrent processing tasks |
| **Buffer Statistics** | Real-time monitoring of buffer state |

### Key Benefits

- ✅ **Non-blocking emission** - Producers don't wait for pipeline completion
- ✅ **Backpressure control** - Handle overload scenarios gracefully
- ✅ **Higher throughput** - Batch processing and parallel consumers
- ✅ **Backward compatible** - Opt-in features, existing code works unchanged

---

## Quick Start

### Basic Async Stream (Default Behavior)

Without any performance configuration, streams work synchronously:

```csharp
var stream = StreamBuilder<int>.CreateNewStream("BasicStream")
    .Stream()
    .Map(x => x * 2)
    .Sink(Console.WriteLine)
    .Build();

stream.Start();
stream.Emit(42);        // Blocks until processing completes
await stream.EmitAsync(42); // Runs on thread pool, still waits
stream.Stop();
```

### Buffered Async Stream (High Performance)

Enable buffered processing for non-blocking emission:

```csharp
var stream = StreamBuilder<int>.CreateNewStream("FastStream")
    .WithPerformanceOptions(new StreamPerformanceOptions
    {
        EnableBufferedProcessing = true,
        BufferCapacity = 10_000,
        BackpressureStrategy = BackpressureStrategy.Block
    })
    .Stream()
    .Map(x => x * 2)
    .Sink(ProcessItem)
    .Build();

stream.Start();

// Non-blocking - returns immediately after buffering
stream.EmitAndForget(42);

// Async - awaits buffering (not processing)
await stream.EmitAsync(42);

// Graceful shutdown - waits for buffer to drain
await stream.StopAsync();
```

---

## Configuration Options

### StreamPerformanceOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableBufferedProcessing` | `bool` | `false` | Enable internal buffer and async consumers |
| `BufferCapacity` | `int` | `10,000` | Maximum items in buffer |
| `BackpressureStrategy` | `BackpressureStrategy` | `Block` | Behavior when buffer is full |
| `BatchSize` | `int` | `1` | Items per batch (1 = immediate processing) |
| `BatchTimeout` | `TimeSpan` | `100ms` | Max wait time for batch to fill |
| `ConcurrencyLevel` | `int` | `1` | Number of parallel consumer tasks |
| `BlockingTimeout` | `TimeSpan` | `30s` | Timeout for blocking operations |
| `OnItemDropped` | `Action<object, DropReason>` | `null` | Callback when items are dropped |

### Example: Custom Configuration

```csharp
var options = new StreamPerformanceOptions
{
    EnableBufferedProcessing = true,
    BufferCapacity = 50_000,
    BackpressureStrategy = BackpressureStrategy.DropOldest,
    BatchSize = 100,
    BatchTimeout = TimeSpan.FromMilliseconds(50),
    ConcurrencyLevel = Environment.ProcessorCount,
    BlockingTimeout = TimeSpan.FromSeconds(60),
    OnItemDropped = (item, reason) => 
        Console.WriteLine($"Dropped: {item}, Reason: {reason}")
};

var stream = StreamBuilder<Event>.CreateNewStream("CustomStream")
    .WithPerformanceOptions(options)
    .Stream()
    // ... pipeline
    .Build();
```

---

## Backpressure Strategies

When the internal buffer reaches capacity, the backpressure strategy determines behavior:

### Block (Default)

Waits for space to become available. Best for scenarios where data loss is unacceptable.

```csharp
BackpressureStrategy = BackpressureStrategy.Block,
BlockingTimeout = TimeSpan.FromSeconds(30) // Throws after timeout
```

**Behavior:**
- `EmitAsync` blocks (asynchronously) until space available
- `EmitAndForget` blocks synchronously
- Throws `OperationCanceledException` on timeout

### DropNewest

Silently drops incoming items when buffer is full. Best for real-time data where latest data is more important.

```csharp
BackpressureStrategy = BackpressureStrategy.DropNewest,
OnItemDropped = (item, reason) => metrics.IncrementDropped()
```

**Behavior:**
- Writes always succeed (return `true`)
- Excess items are silently dropped
- Use `OnItemDropped` callback to track dropped items

### DropOldest

Removes oldest items to make room for new ones. Best for keeping the most recent data.

```csharp
BackpressureStrategy = BackpressureStrategy.DropOldest,
OnItemDropped = (item, reason) => LogDropped(item)
```

**Behavior:**
- New items always accepted
- Oldest buffered items are evicted
- Callback receives evicted items

### ThrowException

Throws `BufferFullException` when buffer is full. Best for explicit failure handling.

```csharp
BackpressureStrategy = BackpressureStrategy.ThrowException
```

**Behavior:**
- `EmitAndForget` throws `BufferFullException`
- `EmitAsync` throws `BufferFullException`
- Caller must handle the exception

```csharp
try
{
    stream.EmitAndForget(item);
}
catch (BufferFullException ex)
{
    Console.WriteLine($"Buffer full! Capacity: {ex.BufferCapacity}");
    // Implement retry logic or alternative handling
}
```

---

## Emit Methods

### Emit (Synchronous)

Blocks until the entire pipeline processes the item. Unchanged from previous behavior.

```csharp
stream.Emit(item); // Blocks until Sink completes
```

### EmitAsync

Asynchronously emits an item:
- **Without buffering**: Runs pipeline on thread pool, awaits completion
- **With buffering**: Awaits buffer space, returns when buffered (not processed)

```csharp
await stream.EmitAsync(item);
await stream.EmitAsync(item, cancellationToken);
```

### EmitAndForget

Fire-and-forget emission. **Requires buffered processing enabled.**

```csharp
bool accepted = stream.EmitAndForget(item);
// Returns immediately
// accepted = true if buffered, false if dropped (DropNewest/DropOldest)
// Throws BufferFullException if strategy is ThrowException
```

### EmitBatchAsync

Efficiently emit multiple items:

```csharp
var items = Enumerable.Range(1, 1000);
await stream.EmitBatchAsync(items);
await stream.EmitBatchAsync(items, cancellationToken);
```

---

## Monitoring & Statistics

### GetBufferStatistics

Returns real-time buffer metrics (only when buffered processing is enabled):

```csharp
BufferStatistics stats = stream.GetBufferStatistics();

if (stats != null)
{
    Console.WriteLine($"Current Count: {stats.CurrentCount}");
    Console.WriteLine($"Capacity: {stats.Capacity}");
    Console.WriteLine($"Utilization: {stats.UtilizationPercent:F1}%");
    Console.WriteLine($"Total Enqueued: {stats.TotalEnqueued}");
    Console.WriteLine($"Total Processed: {stats.TotalProcessed}");
    Console.WriteLine($"Total Dropped: {stats.TotalDropped}");
}
```

### BufferStatistics Properties

| Property | Type | Description |
|----------|------|-------------|
| `CurrentCount` | `int` | Items currently in buffer |
| `Capacity` | `int` | Maximum buffer capacity |
| `TotalEnqueued` | `long` | Total items added since start |
| `TotalProcessed` | `long` | Total items successfully processed |
| `TotalDropped` | `long` | Total items dropped due to backpressure |
| `UtilizationPercent` | `double` | Current buffer utilization (0-100) |

### Health Monitoring Example

```csharp
// Periodic monitoring
var timer = new Timer(_ =>
{
    var stats = stream.GetBufferStatistics();
    if (stats != null && stats.UtilizationPercent > 80)
    {
        logger.Warn($"Buffer utilization high: {stats.UtilizationPercent:F1}%");
    }
}, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
```

---

## Preset Configurations

### HighThroughput

Optimized for maximum throughput with parallel processing:

```csharp
var options = StreamPerformanceOptions.HighThroughput(
    bufferCapacity: 100_000,      // Default: 100,000
    concurrencyLevel: 8           // Default: Environment.ProcessorCount
);

// Equivalent to:
new StreamPerformanceOptions
{
    EnableBufferedProcessing = true,
    BufferCapacity = 100_000,
    BackpressureStrategy = BackpressureStrategy.Block,
    BatchSize = 100,
    BatchTimeout = TimeSpan.FromMilliseconds(50),
    ConcurrencyLevel = 8,
    BlockingTimeout = TimeSpan.FromSeconds(60)
}
```

**Best for:** Log ingestion, metrics collection, high-volume event processing

### LowLatency

Optimized for minimal latency with immediate processing:

```csharp
var options = StreamPerformanceOptions.LowLatency(
    bufferCapacity: 10_000        // Default: 10,000
);

// Equivalent to:
new StreamPerformanceOptions
{
    EnableBufferedProcessing = true,
    BufferCapacity = 10_000,
    BackpressureStrategy = BackpressureStrategy.Block,
    BatchSize = 1,                // Process immediately
    ConcurrencyLevel = 1,         // Single consumer for ordering
    BlockingTimeout = TimeSpan.FromSeconds(30)
}
```

**Best for:** Real-time notifications, interactive applications

### DropOldest

Optimized for scenarios where latest data matters most:

```csharp
var options = StreamPerformanceOptions.DropOldest(
    bufferCapacity: 10_000,
    onItemDropped: (item, reason) => metrics.Track("dropped", item)
);

// Equivalent to:
new StreamPerformanceOptions
{
    EnableBufferedProcessing = true,
    BufferCapacity = 10_000,
    BackpressureStrategy = BackpressureStrategy.DropOldest,
    BatchSize = 1,
    ConcurrencyLevel = 1,
    OnItemDropped = onItemDropped
}
```

**Best for:** Stock tickers, sensor data, real-time dashboards

---

## Best Practices

### 1. Choose the Right Backpressure Strategy

| Scenario | Recommended Strategy |
|----------|---------------------|
| Financial transactions | `Block` - Never lose data |
| Real-time metrics | `DropOldest` - Keep latest |
| Log aggregation | `Block` with large buffer |
| Live video frames | `DropNewest` - Skip if behind |
| Critical alerts | `ThrowException` - Explicit handling |

### 2. Size Your Buffer Appropriately

```csharp
// Rule of thumb: Buffer should hold 2-5 seconds of peak throughput
var peakItemsPerSecond = 10_000;
var bufferSeconds = 3;
var bufferCapacity = peakItemsPerSecond * bufferSeconds; // 30,000
```

### 3. Use Batch Processing for High Throughput

```csharp
// For I/O-bound sinks (database, network)
var options = new StreamPerformanceOptions
{
    EnableBufferedProcessing = true,
    BatchSize = 100,                    // Batch writes
    BatchTimeout = TimeSpan.FromMilliseconds(100)
};
```

### 4. Monitor Buffer Health

```csharp
// Set up alerts
if (stats.UtilizationPercent > 90)
{
    // Scale up consumers or reduce input rate
}

if (stats.TotalDropped > previousDropped)
{
    // Data loss occurring - investigate
}
```

### 5. Graceful Shutdown

Always use `StopAsync` to ensure all buffered items are processed:

```csharp
// ❌ Bad - may lose buffered items
stream.Stop();

// ✅ Good - waits for buffer to drain
await stream.StopAsync();

// ✅ Good - with timeout
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await stream.StopAsync(cts.Token);
```

### 6. Thread Safety with Parallel Consumers

When using `ConcurrencyLevel > 1`, ensure your operators are thread-safe:

```csharp
// ❌ Bad - not thread-safe
var list = new List<int>();
.Sink(x => list.Add(x))

// ✅ Good - thread-safe collection
var bag = new ConcurrentBag<int>();
.Sink(x => bag.Add(x))

// ✅ Good - atomic operations
var counter = 0;
.Sink(x => Interlocked.Increment(ref counter))
```

---

## Migration Guide

### From Synchronous to Async (Minimal Change)

Existing code continues to work unchanged:

```csharp
// Before (still works)
stream.Emit(item);

// After (same behavior, async wrapper)
await stream.EmitAsync(item);
```

### Enabling Buffered Processing

Add performance options without changing pipeline logic:

```csharp
// Before
var stream = StreamBuilder<int>.CreateNewStream("MyStream")
    .Stream()
    .Map(x => x * 2)
    .Sink(ProcessItem)
    .Build();

// After (just add WithPerformanceOptions)
var stream = StreamBuilder<int>.CreateNewStream("MyStream")
    .WithPerformanceOptions(StreamPerformanceOptions.LowLatency())
    .Stream()
    .Map(x => x * 2)
    .Sink(ProcessItem)
    .Build();
```

### Updating Stop Calls

Replace synchronous stop with async for graceful shutdown:

```csharp
// Before
stream.Stop();

// After
await stream.StopAsync();
```

---

## API Reference

### IStream&lt;TIn, TCurrent&gt; Methods

| Method | Description |
|--------|-------------|
| `void Emit(TIn value)` | Synchronous emission (blocks until processed) |
| `Task EmitAsync(TIn value, CancellationToken ct)` | Async emission |
| `Task EmitBatchAsync(IEnumerable<TIn> values, CancellationToken ct)` | Batch emission |
| `bool EmitAndForget(TIn value)` | Fire-and-forget (requires buffering) |
| `void Start()` | Start the stream |
| `void Stop()` | Stop immediately |
| `Task StopAsync(CancellationToken ct)` | Graceful async stop |
| `BufferStatistics GetBufferStatistics()` | Get buffer metrics (null if no buffering) |

### IInitialStreamBuilder&lt;TIn&gt; Methods

| Method | Description |
|--------|-------------|
| `WithPerformanceOptions(StreamPerformanceOptions)` | Configure performance options |
| `WithErrorHandling(StreamExecutionOptions)` | Configure error handling |
| `WithTelemetry(ITelemetryProvider)` | Configure telemetry |

### Exceptions

| Exception | When Thrown |
|-----------|-------------|
| `BufferFullException` | Buffer full with `ThrowException` strategy |
| `OperationCanceledException` | Blocking timeout or cancellation |
| `InvalidOperationException` | `EmitAndForget` without buffering enabled |

---

## Complete Example

```csharp
using Cortex.Streams;
using Cortex.Streams.Performance;

public class OrderProcessor
{
    private readonly IStream<Order, ProcessedOrder> _stream;

    public OrderProcessor()
    {
        _stream = StreamBuilder<Order>.CreateNewStream("OrderProcessor")
            .WithPerformanceOptions(new StreamPerformanceOptions
            {
                EnableBufferedProcessing = true,
                BufferCapacity = 50_000,
                BackpressureStrategy = BackpressureStrategy.Block,
                ConcurrencyLevel = 4,
                OnItemDropped = (item, reason) => 
                    Logger.Warn($"Order dropped: {((Order)item).Id}")
            })
            .WithErrorHandling(new StreamExecutionOptions
            {
                ErrorHandlingStrategy = ErrorHandlingStrategy.Retry,
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1)
            })
            .Stream()
            .Filter(order => order.IsValid)
            .Map(order => EnrichOrder(order))
            .Map(order => ProcessOrder(order))
            .Sink(order => SaveToDatabase(order))
            .Build();
    }

    public void Start() => _stream.Start();

    public async Task StopAsync() => await _stream.StopAsync();

    public async Task SubmitOrderAsync(Order order)
    {
        await _stream.EmitAsync(order);
    }

    public void SubmitOrderFireAndForget(Order order)
    {
        if (!_stream.EmitAndForget(order))
        {
            Logger.Warn($"Order {order.Id} was dropped");
        }
    }

    public void LogStats()
    {
        var stats = _stream.GetBufferStatistics();
        if (stats != null)
        {
            Logger.Info($"Buffer: {stats.CurrentCount}/{stats.Capacity} " +
                       $"({stats.UtilizationPercent:F1}%), " +
                       $"Processed: {stats.TotalProcessed}, " +
                       $"Dropped: {stats.TotalDropped}");
        }
    }
}
```