# FanOut - Multiple Sinks Pattern

**FanOut** is a powerful stream processing pattern in Cortex.Streams that allows you to send the same data to multiple destinations (sinks) simultaneously. This is essential for scenarios like dual-writes, multi-channel notifications, real-time analytics, and audit logging.

## Table of Contents

- [Overview](#overview)
- [When to Use FanOut](#when-to-use-fanout)
- [FanOut vs AddBranch](#fanout-vs-addbranch)
- [Basic Usage](#basic-usage)
- [API Reference](#api-reference)
- [Real-World Examples](#real-world-examples)
  - [E-Commerce Order Processing](#e-commerce-order-processing)
  - [IoT Sensor Data Processing](#iot-sensor-data-processing)
  - [Log Aggregation Pipeline](#log-aggregation-pipeline)
  - [Financial Transaction Processing](#financial-transaction-processing)
  - [User Activity Tracking](#user-activity-tracking)
- [Best Practices](#best-practices)
- [Error Handling](#error-handling)
- [Performance Considerations](#performance-considerations)

---

## Overview

The FanOut pattern enables a stream to broadcast data to multiple sinks in parallel. Each sink operates independently and can optionally have its own filter predicate to receive only matching data.

```
                    ???????????????????
                    ?    Database     ?
                    ???????????????????
                            ?
                            ?
????????????    ?????????????????????????    ???????????????????
?  Source  ??????       FanOut          ??????     Kafka       ?
????????????    ?????????????????????????    ???????????????????
                            ?
                            ?
                    ???????????????????
                    ?     Alerts      ?
                    ?  (filtered)     ?
                    ???????????????????
```

## When to Use FanOut

Use FanOut when you need to:

- **Dual-write** to multiple storage systems (database + cache)
- **Publish events** to multiple message brokers
- **Send notifications** to multiple channels (email, SMS, push)
- **Feed multiple analytics** systems from the same data source
- **Create audit trails** while processing data
- **Route data conditionally** to different sinks based on criteria

## FanOut vs AddBranch

| Feature | `FanOut` | `AddBranch` |
|---------|----------|-------------|
| **Purpose** | Multiple sinks, same or filtered data | Complex branching with transformations |
| **API Style** | Fluent, focused on sinks | Configuration-based, full pipeline control |
| **Transformations** | Per-sink via `ToWithTransform` | Full pipeline per branch |
| **Use Case** | Simple fan-out to multiple destinations | Different processing logic per branch |
| **Complexity** | Simple | More flexible but complex |

**Rule of thumb:** Use `FanOut` when you need multiple sinks with optional filtering. Use `AddBranch` when each branch needs different transformation logic.

## Basic Usage

### Simple Multi-Sink

```csharp
var stream = StreamBuilder<Order>.CreateNewStream("OrderProcessor")
    .Stream()
    .FanOut(fanOut => fanOut
        .To("database", order => SaveToDatabase(order))
        .To("kafka", order => PublishToKafka(order))
        .To("logging", order => LogOrder(order)))
    .Build();

stream.Start();
stream.Emit(new Order { Id = "ORD-001", Amount = 100 });
```

### With Filtering

```csharp
var stream = StreamBuilder<Order>.CreateNewStream("OrderProcessor")
    .Stream()
    .FanOut(fanOut => fanOut
        // All orders to database
        .To("database", order => SaveToDatabase(order))
        // Only high-value orders to alerts
        .To("alerts", 
            order => order.Amount > 10000, 
            order => SendAlert(order))
        // Only priority orders to fast-track queue
        .To("priority-queue",
            order => order.IsPriority,
            order => EnqueuePriority(order)))
    .Build();
```

### With Transformation

```csharp
var stream = StreamBuilder<Order>.CreateNewStream("OrderProcessor")
    .Stream()
    .FanOut(fanOut => fanOut
        // Store original order
        .To("database", order => SaveOrder(order))
        // Transform to event for Kafka
        .ToWithTransform("kafka",
            order => new OrderEvent(order.Id, "Created", DateTime.UtcNow),
            evt => PublishEvent(evt))
        // Transform to metrics for analytics
        .ToWithTransform("analytics",
            order => new OrderMetrics(order.Id, order.Amount),
            metrics => RecordMetrics(metrics)))
    .Build();
```

## API Reference

### `IFanOutBuilder<TIn, TCurrent>`

| Method | Description |
|--------|-------------|
| `To(string name, Action<TCurrent> sinkFunction)` | Adds a named sink that receives all data |
| `To(string name, Func<TCurrent, bool> predicate, Action<TCurrent> sinkFunction)` | Adds a filtered sink |
| `To(string name, ISinkOperator<TCurrent> sinkOperator)` | Adds a custom sink operator |
| `To(string name, Func<TCurrent, bool> predicate, ISinkOperator<TCurrent> sinkOperator)` | Adds a filtered custom sink operator |
| `ToWithTransform<TOutput>(string name, Func<TCurrent, TOutput> mapFunction, Action<TOutput> sinkFunction)` | Adds a sink with per-sink transformation |
| `Build()` | Builds the stream with all configured sinks |

### Important Notes

- **Sink names must be unique** within a FanOut
- **At least one sink** must be configured before calling `Build()`
- Sinks are executed in parallel (order not guaranteed)
- Each sink is independent - one sink's failure doesn't affect others (with proper error handling)

---

## Real-World Examples

### E-Commerce Order Processing

Process orders and distribute to multiple systems simultaneously:

```csharp
public class OrderProcessor
{
    private readonly IOrderRepository _repository;
    private readonly IKafkaProducer _kafka;
    private readonly IAlertService _alerts;
    private readonly IAnalyticsService _analytics;

    public void SetupPipeline()
    {
        var stream = StreamBuilder<Order>.CreateNewStream("OrderProcessingPipeline")
            .Stream()
            // Only process confirmed orders
            .Filter(order => order.Status == OrderStatus.Confirmed)
            .FanOut(fanOut => fanOut
                // 1. Persist to database (primary storage)
                .To("database", order => 
                    _repository.SaveAsync(order).GetAwaiter().GetResult())
                
                // 2. Publish to Kafka for downstream consumers
                .ToWithTransform("kafka-events",
                    order => new OrderConfirmedEvent
                    {
                        OrderId = order.Id,
                        CustomerId = order.CustomerId,
                        Amount = order.TotalAmount,
                        Timestamp = DateTime.UtcNow
                    },
                    evt => _kafka.PublishAsync("order-events", evt).GetAwaiter().GetResult())
                
                // 3. Alert on high-value orders (> $10,000)
                .To("high-value-alerts",
                    order => order.TotalAmount > 10000,
                    order => _alerts.SendHighValueOrderAlert(order))
                
                // 4. Send metrics to analytics
                .ToWithTransform("analytics",
                    order => new OrderMetrics
                    {
                        OrderId = order.Id,
                        Amount = order.TotalAmount,
                        ItemCount = order.Items.Count,
                        Region = order.ShippingAddress.Region
                    },
                    metrics => _analytics.RecordOrderMetrics(metrics)))
            .Build();

        stream.Start();
        return stream;
    }
}
```

### IoT Sensor Data Processing

Route sensor data to different storage tiers based on criticality:

```csharp
public class SensorDataPipeline
{
    public IStream<SensorReading, SensorReading> Create()
    {
        return StreamBuilder<SensorReading>.CreateNewStream("SensorDataPipeline")
            .Stream()
            .FanOut(fanOut => fanOut
                // Archive ALL readings to cold storage
                .To("cold-storage", reading => 
                    _coldStorage.Archive(reading))
                
                // Critical readings (temp > 100°C) - immediate alert
                .To("critical-alerts",
                    reading => reading.Temperature > 100,
                    reading => 
                    {
                        _pagerDuty.TriggerAlert($"CRITICAL: {reading.SensorId} at {reading.Temperature}°C");
                        _hotStorage.Store(reading); // Also store in hot storage
                    })
                
                // Warning readings (80-100°C) - log for review
                .To("warning-log",
                    reading => reading.Temperature >= 80 && reading.Temperature <= 100,
                    reading => _warningLogger.Log(reading))
                
                // Normal readings to time-series DB
                .To("timeseries-db",
                    reading => reading.Temperature < 80,
                    reading => _timeSeriesDb.Insert(reading))
                
                // All readings to real-time dashboard
                .ToWithTransform("dashboard",
                    reading => new DashboardUpdate
                    {
                        SensorId = reading.SensorId,
                        Value = reading.Temperature,
                        Status = GetStatus(reading.Temperature),
                        Timestamp = reading.Timestamp
                    },
                    update => _dashboard.Push(update)))
            .Build();
    }

    private string GetStatus(double temp) => temp switch
    {
        > 100 => "CRITICAL",
        >= 80 => "WARNING",
        _ => "NORMAL"
    };
}
```

### Log Aggregation Pipeline

Aggregate logs from multiple services and route by severity:

```csharp
public class LogAggregationPipeline
{
    public IStream<LogEntry, LogEntry> Create()
    {
        return StreamBuilder<LogEntry>.CreateNewStream("LogAggregation")
            .Stream()
            // Enrich logs with metadata
            .Map(log => log with 
            { 
                ProcessedAt = DateTime.UtcNow,
                Environment = Environment.GetEnvironmentVariable("ENV") 
            })
            .FanOut(fanOut => fanOut
                // All logs to Elasticsearch for search
                .To("elasticsearch", log => 
                    _elasticsearch.IndexAsync("logs", log).GetAwaiter().GetResult())
                
                // Errors to PagerDuty for on-call
                .To("pagerduty",
                    log => log.Level == LogLevel.Error,
                    log => _pagerDuty.CreateIncident(new Incident
                    {
                        Title = $"[{log.Service}] {log.Message}",
                        Severity = "high",
                        Details = log.StackTrace
                    }))
                
                // Warnings to Slack channel
                .To("slack",
                    log => log.Level == LogLevel.Warning,
                    log => _slack.PostMessage("#alerts", 
                        $"?? [{log.Service}] {log.Message}"))
                
                // Metrics to Prometheus
                .ToWithTransform("prometheus",
                    log => new LogMetric
                    {
                        Service = log.Service,
                        Level = log.Level.ToString(),
                        Count = 1
                    },
                    metric => _prometheus.IncrementCounter(
                        "log_entries_total",
                        new[] { metric.Service, metric.Level }))
                
                // Long-term archive to S3
                .To("s3-archive",
                    log => _s3.PutObjectAsync($"logs/{log.Timestamp:yyyy/MM/dd}/{log.Id}.json", 
                        JsonSerializer.Serialize(log)).GetAwaiter().GetResult()))
            .Build();
    }
}
```

### Financial Transaction Processing

Process transactions with multiple compliance checks:

```csharp
public class TransactionPipeline
{
    public IStream<Transaction, Transaction> Create()
    {
        return StreamBuilder<Transaction>.CreateNewStream("TransactionProcessing")
            .Stream()
            .FanOut(fanOut => fanOut
                // Main ledger - all transactions
                .To("ledger", txn => 
                    _ledger.RecordTransaction(txn))
                
                // Fraud detection - suspicious patterns
                .To("fraud-detection",
                    txn => txn.Amount > 10000 && txn.Type == TransactionType.International,
                    txn => _fraudService.AnalyzeAsync(txn).GetAwaiter().GetResult())
                
                // Regulatory reporting - CTR for transactions > $10,000
                .To("regulatory-ctr",
                    txn => txn.Amount > 10000,
                    txn => _compliance.FileCTR(new CurrencyTransactionReport
                    {
                        TransactionId = txn.Id,
                        Amount = txn.Amount,
                        AccountHolder = txn.AccountHolder,
                        FilingDate = DateTime.UtcNow
                    }))
                
                // Sanctions screening - international transactions
                .To("sanctions-screening",
                    txn => txn.Type == TransactionType.International,
                    txn => _sanctions.ScreenTransaction(txn))
                
                // Audit trail - immutable record
                .ToWithTransform("audit-log",
                    txn => new AuditEntry
                    {
                        EntityType = "Transaction",
                        EntityId = txn.Id,
                        Action = "PROCESSED",
                        Timestamp = DateTime.UtcNow,
                        Details = JsonSerializer.Serialize(txn)
                    },
                    entry => _auditLog.Append(entry))
                
                // Real-time balance update
                .To("balance-service",
                    txn => _balanceService.UpdateBalance(txn.AccountId, txn.Amount)))
            .Build();
    }
}
```

### User Activity Tracking

Track user activity across multiple analytics platforms:

```csharp
public class UserActivityPipeline
{
    public IStream<UserActivity, UserActivity> Create()
    {
        return StreamBuilder<UserActivity>.CreateNewStream("UserActivityTracking")
            .Stream()
            .FanOut(fanOut => fanOut
                // Raw clickstream to data lake
                .To("data-lake", activity => 
                    _dataLake.Store("clickstream", activity))
                
                // Page views to Google Analytics
                .To("google-analytics",
                    activity => activity.Type == ActivityType.PageView,
                    activity => _ga.TrackPageView(activity.UserId, activity.PageUrl))
                
                // Purchases to commerce analytics
                .To("commerce-analytics",
                    activity => activity.Type == ActivityType.Purchase,
                    activity => _commerce.TrackPurchase(new PurchaseEvent
                    {
                        UserId = activity.UserId,
                        ProductId = activity.Details,
                        Timestamp = activity.Timestamp
                    }))
                
                // Search queries for search optimization
                .To("search-analytics",
                    activity => activity.Type == ActivityType.Search,
                    activity => _searchAnalytics.RecordQuery(activity.Details))
                
                // Session metrics for engagement
                .ToWithTransform("session-metrics",
                    activity => new SessionMetric
                    {
                        SessionId = activity.SessionId,
                        UserId = activity.UserId,
                        ActivityCount = 1,
                        LastActivity = activity.Timestamp
                    },
                    metric => _sessions.UpdateMetrics(metric))
                
                // Real-time personalization engine
                .To("personalization",
                    activity => _personalization.UpdateUserProfile(activity)))
            .Build();
    }
}
```

---

## Best Practices

### 1. Use Meaningful Sink Names

```csharp
// ? Good - descriptive names
.To("order-database", ...)
.To("kafka-order-events", ...)
.To("high-value-alerts", ...)

// ? Avoid - generic names
.To("sink1", ...)
.To("output", ...)
```

### 2. Handle Errors Per Sink

```csharp
.To("database", order => 
{
    try 
    {
        _repository.Save(order);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to save order {OrderId}", order.Id);
        _deadLetterQueue.Enqueue(order);
    }
})
```

### 3. Use Async Patterns Appropriately

```csharp
// For I/O-bound operations, consider async wrappers
.To("database", order => 
    _repository.SaveAsync(order).GetAwaiter().GetResult())
```

### 4. Apply Filters at FanOut Level

```csharp
// ? Good - filter at FanOut level
.FanOut(fanOut => fanOut
    .To("alerts", order => order.Amount > 10000, order => SendAlert(order)))

// ? Less efficient - filtering inside sink
.FanOut(fanOut => fanOut
    .To("alerts", order => 
    {
        if (order.Amount > 10000) // Avoid this
            SendAlert(order);
    }))
```

### 5. Keep Transformations Simple

```csharp
// ? Good - simple transformation
.ToWithTransform("events", 
    order => new OrderEvent(order.Id, order.Status),
    evt => Publish(evt))

// ? Avoid - complex logic in transform
.ToWithTransform("events",
    order => 
    {
        // Don't put complex logic here
        var result = ComplexCalculation(order);
        return new OrderEvent(result);
    },
    evt => Publish(evt))
```

---

## Error Handling

Configure error handling at the stream level:

```csharp
var stream = StreamBuilder<Order>.CreateNewStream("OrderProcessor")
    .WithErrorHandling(options => 
    {
        options.OnError = (ex, item) => 
        {
            _logger.LogError(ex, "Error processing {Item}", item);
            return ErrorAction.Continue; // or Skip, Retry, Stop
        };
        options.MaxRetries = 3;
        options.RetryDelay = TimeSpan.FromSeconds(1);
    })
    .Stream()
    .FanOut(fanOut => fanOut
        .To("database", order => SaveOrder(order))
        .To("kafka", order => PublishEvent(order)))
    .Build();
```

---

## Performance Considerations

1. **Sink Execution**: Sinks execute sequentially within the FanOut operator. For high-throughput scenarios, consider using async patterns or dedicated thread pools.

2. **Filter Early**: Apply filters to reduce data volume before expensive operations.

3. **Monitor Sink Latency**: Use telemetry to identify slow sinks that may become bottlenecks.

4. **Consider Buffering**: For bursty workloads, consider adding buffering before slow sinks.

```csharp
// Example: Monitor FanOut performance
var stream = StreamBuilder<Order>.CreateNewStream("OrderProcessor")
    .WithTelemetry(telemetryProvider)  // Enables per-sink metrics
    .Stream()
    .FanOut(fanOut => fanOut
        .To("fast-sink", order => FastOperation(order))
        .To("slow-sink", order => SlowOperation(order)))
    .Build();
```

---

## Related Documentation

- [Stream Processing Basics](./stream-processing-basics.md)
- [Branching with AddBranch](./branching.md)
- [Error Handling](./error-handling.md)
- [Telemetry and Monitoring](./telemetry.md)
