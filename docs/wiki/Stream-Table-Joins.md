# Stream-Table Joins in Cortex Data Framework

This guide covers how to use **Stream-Table Joins** in Cortex Data Framework to enrich streaming data with reference data stored in state stores.

## Overview

Stream-Table joins allow you to enrich events from a stream by looking up related data in a table (state store). This is useful when you have:
- A stream of events (orders, clicks, transactions)
- A table of reference data (customers, products, configurations)

Cortex.Streams provides two types of Stream-Table joins:

| Join Type | Behavior | Use Case |
|-----------|----------|----------|
| **Inner Join** (`Join`) | Only emits when a match is found | Required enrichment data |
| **Left Join** (`LeftJoin`) | Always emits, with `null` if no match | Optional enrichment data |

---

## Inner Join (Stream-Table)

The inner join only emits results when the stream element's key matches an entry in the table.

### Example: Order Processing with Required Customer Data

```csharp
// Define models
public record Order(string OrderId, int CustomerId, decimal Amount, DateTime Timestamp);
public record Customer(int Id, string Name, string Email, string Tier);
public record EnrichedOrder(string OrderId, decimal Amount, string CustomerName, string CustomerTier);

// Create and populate the customer table
var customerStore = new InMemoryStateStore<int, Customer>("CustomerStore");
customerStore.Put(1001, new Customer(1001, "Alice Smith", "alice@example.com", "Gold"));
customerStore.Put(1002, new Customer(1002, "Bob Johnson", "bob@example.com", "Silver"));
customerStore.Put(1003, new Customer(1003, "Carol Williams", "carol@example.com", "Bronze"));

// Build the stream with inner join
var orderStream = StreamBuilder<Order>.CreateNewStream("OrderEnrichmentStream")
    .Stream()
    .Join(
        customerStore,
        order => order.CustomerId,  // Key selector
        (order, customer) => new EnrichedOrder(
            order.OrderId,
            order.Amount,
            customer.Name,
            customer.Tier))
    .Sink(enrichedOrder => 
    {
        Console.WriteLine($"Processing order {enrichedOrder.OrderId} for {enrichedOrder.CustomerName} ({enrichedOrder.CustomerTier})");
        // Apply tier-based discount, send confirmation email, etc.
    })
    .Build();

// Start and emit orders
orderStream.Start();
orderStream.Emit(new Order("ORD-001", 1001, 150.00m, DateTime.UtcNow)); // ✅ Emits - Alice exists
orderStream.Emit(new Order("ORD-002", 1002, 75.50m, DateTime.UtcNow));  // ✅ Emits - Bob exists
orderStream.Emit(new Order("ORD-003", 9999, 200.00m, DateTime.UtcNow)); // ❌ Dropped - Customer 9999 not found
```

**Output:**
```
Processing order ORD-001 for Alice Smith (Gold)
Processing order ORD-002 for Bob Johnson (Silver)
```

> ⚠️ **Note:** Order ORD-003 is silently dropped because customer 9999 doesn't exist in the table.

---

## Left Join (Stream-Table)

The left join **always** emits a result for every stream element, even when no matching table entry exists. When there's no match, the right side value is `null` (or `default`).

### Example: IoT Sensor Data with Optional Device Metadata

```csharp
// Define models
public record SensorReading(string SensorId, double Value, string Unit, DateTime Timestamp);
public record DeviceInfo(string SensorId, string Location, string Owner, DateTime InstalledAt);
public record EnrichedReading(
    string SensorId, 
    double Value, 
    string Unit, 
    string? Location, 
    string? Owner,
    bool HasDeviceInfo);

// Device registry - may not have all sensors registered
var deviceRegistry = new InMemoryStateStore<string, DeviceInfo>("DeviceRegistry");
deviceRegistry.Put("SENSOR-001", new DeviceInfo("SENSOR-001", "Building A, Floor 2", "Facilities Team", DateTime.Parse("2023-01-15")));
deviceRegistry.Put("SENSOR-002", new DeviceInfo("SENSOR-002", "Building B, Floor 1", "IT Department", DateTime.Parse("2023-03-20")));
// Note: SENSOR-003 is NOT registered

// Build stream with left join
var sensorStream = StreamBuilder<SensorReading>.CreateNewStream("SensorEnrichmentStream")
    .Stream()
    .LeftJoin(
        deviceRegistry,
        reading => reading.SensorId,
        (reading, device) => new EnrichedReading(
            reading.SensorId,
            reading.Value,
            reading.Unit,
            device?.Location,      // May be null
            device?.Owner,         // May be null
            device != null))       // Flag indicating if device info was found
    .Sink(enriched =>
    {
        if (enriched.HasDeviceInfo)
        {
            Console.WriteLine($"[{enriched.Location}] {enriched.SensorId}: {enriched.Value} {enriched.Unit}");
        }
        else
        {
            Console.WriteLine($"[UNKNOWN DEVICE] {enriched.SensorId}: {enriched.Value} {enriched.Unit} - Please register this device!");
        }
    })
    .Build();

// Start and emit sensor readings
sensorStream.Start();
sensorStream.Emit(new SensorReading("SENSOR-001", 23.5, "°C", DateTime.UtcNow));  // ✅ Has device info
sensorStream.Emit(new SensorReading("SENSOR-002", 45.2, "%", DateTime.UtcNow));   // ✅ Has device info
sensorStream.Emit(new SensorReading("SENSOR-003", 1013.25, "hPa", DateTime.UtcNow)); // ✅ Emits with null device info
```

**Output:**
```
[Building A, Floor 2] SENSOR-001: 23.5 °C
[Building B, Floor 1] SENSOR-002: 45.2 %
[UNKNOWN DEVICE] SENSOR-003: 1013.25 hPa - Please register this device!
```

---

## Real-World Use Cases

### 1. E-Commerce: Product Catalog Enrichment

```csharp
public record CartItem(string SessionId, string ProductSku, int Quantity);
public record Product(string Sku, string Name, decimal Price, int StockLevel);
public record EnrichedCartItem(string SessionId, string ProductName, decimal UnitPrice, decimal TotalPrice, bool InStock);

var productCatalog = new InMemoryStateStore<string, Product>("ProductCatalog");
// Load products from database...

var cartStream = StreamBuilder<CartItem>.CreateNewStream("CartEnrichment")
    .Stream()
    .LeftJoin(
        productCatalog,
        item => item.ProductSku,
        (item, product) => new EnrichedCartItem(
            item.SessionId,
            product?.Name ?? "Unknown Product",
            product?.Price ?? 0m,
            (product?.Price ?? 0m) * item.Quantity,
            product?.StockLevel > 0))
    .Filter(item => item.InStock)  // Only process in-stock items
    .Sink(item => ProcessCartItem(item))
    .Build();
```

### 2. Financial Services: Transaction Risk Scoring

```csharp
public record Transaction(string TxId, string AccountId, decimal Amount, string MerchantCategory);
public record AccountProfile(string AccountId, string RiskLevel, decimal DailyLimit, List<string> TrustedCategories);
public record ScoredTransaction(string TxId, decimal Amount, string RiskLevel, bool ExceedsLimit, bool TrustedMerchant);

var accountProfiles = new InMemoryStateStore<string, AccountProfile>("AccountProfiles");

var transactionStream = StreamBuilder<Transaction>.CreateNewStream("TransactionScoring")
    .Stream()
    .LeftJoin(
        accountProfiles,
        tx => tx.AccountId,
        (tx, profile) => new ScoredTransaction(
            tx.TxId,
            tx.Amount,
            profile?.RiskLevel ?? "UNKNOWN",  // Flag unknown accounts
            tx.Amount > (profile?.DailyLimit ?? 0),
            profile?.TrustedCategories?.Contains(tx.MerchantCategory) ?? false))
    .Filter(scored => scored.RiskLevel == "UNKNOWN" || scored.ExceedsLimit || !scored.TrustedMerchant)
    .Sink(scored => AlertFraudTeam(scored))
    .Build();
```

### 3. Gaming: Player Session Enrichment

```csharp
public record GameEvent(string PlayerId, string EventType, Dictionary<string, object> Data);
public record PlayerProfile(string PlayerId, int Level, string Rank, bool IsPremium, DateTime JoinedAt);
public record EnrichedGameEvent(string PlayerId, string EventType, int PlayerLevel, bool IsPremium, Dictionary<string, object> Data);

var playerProfiles = new InMemoryStateStore<string, PlayerProfile>("PlayerProfiles");

var gameEventStream = StreamBuilder<GameEvent>.CreateNewStream("GameEventEnrichment")
    .Stream()
    .LeftJoin(
        playerProfiles,
        evt => evt.PlayerId,
        (evt, player) => new EnrichedGameEvent(
            evt.PlayerId,
            evt.EventType,
            player?.Level ?? 0,
            player?.IsPremium ?? false,
            evt.Data))
    .Sink(evt => 
    {
        // Route to different analytics pipelines based on player status
        if (evt.IsPremium)
            SendToPremiumAnalytics(evt);
        else
            SendToStandardAnalytics(evt);
    })
    .Build();
```

---

## Keeping the Table Updated

The state store can be updated dynamically while the stream is running:

```csharp
// Initial setup
var customerStore = new InMemoryStateStore<int, Customer>("CustomerStore");
var stream = StreamBuilder<Order>.CreateNewStream("Orders")
    .Stream()
    .LeftJoin(customerStore, o => o.CustomerId, (o, c) => new { o, c })
    .Sink(x => Process(x))
    .Build();

stream.Start();

// Update the table from another source (e.g., CDC from database)
Task.Run(async () =>
{
    while (true)
    {
        var updates = await FetchCustomerUpdatesAsync();
        foreach (var customer in updates)
        {
            customerStore.Put(customer.Id, customer);
        }
        await Task.Delay(TimeSpan.FromSeconds(30));
    }
});
```

---

## Using Different State Store Backends

Cortex.Streams supports multiple state store implementations:

```csharp
// In-Memory (default, fast, not persistent)
var memoryStore = new InMemoryStateStore<string, Product>("Products");

// RocksDB (persistent, good for large tables)
var rocksStore = new RocksDbStateStore<string, Product>("Products", "/data/rocksdb");

// Use any store with joins
var stream = StreamBuilder<Order>.CreateNewStream("Orders")
    .Stream()
    .LeftJoin(rocksStore, o => o.ProductId, (o, p) => Enrich(o, p))
    .Sink(Process)
    .Build();
```

---

## Best Practices

1. **Choose the right join type:**
   - Use `Join` (inner) when the reference data is required
   - Use `LeftJoin` when the reference data is optional

2. **Handle null values gracefully:**
   ```csharp
   .LeftJoin(store, keySelector, (left, right) => new Result(
       left.Id,
       right?.Name ?? "Unknown",
       right?.Value ?? defaultValue))
   ```

3. **Pre-populate tables before starting the stream** when possible

4. **Consider table update frequency:**
   - For slowly-changing dimensions: batch updates are fine
   - For fast-changing data: consider Stream-Stream joins instead

5. **Monitor table size** - large tables impact memory usage

---

## See Also

- [Stream-Stream Windowed Joins](./Stream-Stream-Joins.md) - For joining two unbounded streams
- [State Stores](./State-Stores.md) - Different state store backends
- [Windowing](./Windowing.md) - Time-based aggregations
