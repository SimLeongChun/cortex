# Stream-Stream Windowed Joins in Cortex.Streams

This guide covers how to use **Stream-Stream Joins** in Cortex.Streams to correlate events from two different unbounded streams within a time window.

## Overview

Stream-Stream joins allow you to match events from two different streams based on a common key, within a specified time window. This is essential for:
- Correlating events that occur close together in time
- Matching requests with responses
- Joining data from different sources that share a common identifier

### When to Use Stream-Stream Joins

| Scenario | Example |
|----------|---------|
| **Event Correlation** | Match orders with shipments |
| **Request-Response Matching** | Pair API requests with their responses |
| **Cross-System Integration** | Join clicks from web analytics with purchases from POS |
| **Fraud Detection** | Correlate login events with transaction events |
| **IoT Data Fusion** | Combine readings from multiple sensors |

---

## Join Types

Cortex.Streams supports four types of stream-stream joins:

| Join Type | Left Unmatched | Right Unmatched | Use Case |
|-----------|---------------|-----------------|----------|
| **Inner** | Dropped | Dropped | Only care about matched pairs |
| **Left** | Emitted (with null right) | Dropped | Ensure all left events are processed |
| **Right** | Dropped | Emitted (with null left) | Ensure all right events are processed |
| **Outer** | Emitted | Emitted | Process all events from both streams |

---

## Basic Usage

### Example: Matching Orders with Shipments

```csharp
// Define models
public record Order(string OrderId, int CustomerId, decimal Amount, DateTime Timestamp);
public record Shipment(string ShipmentId, string OrderId, string Carrier, DateTime ShippedAt);
public record OrderShipment(Order Order, Shipment? Shipment, bool IsShipped);

// Create the join operator
var joinOperator = new StreamStreamJoinOperator<Order, Shipment, string, OrderShipment>(
    // Key selectors
    order => order.OrderId,
    shipment => shipment.OrderId,
    // Timestamp selectors  
    order => order.Timestamp,
    shipment => shipment.ShippedAt,
    // Join function
    (order, shipment) => new OrderShipment(order, shipment, shipment != null),
    // Configuration: 1-hour window, inner join
    StreamJoinConfiguration.InnerJoin(TimeSpan.FromHours(1)));

// Build the order stream (left side)
var orderStream = StreamBuilder<Order>.CreateNewStream("OrderShipmentJoin")
    .Stream()
    .JoinStream(joinOperator)
    .Sink(result => 
    {
        Console.WriteLine($"Order {result.Order.OrderId} shipped via {result.Shipment?.Carrier}!");
        NotifyCustomer(result.Order.CustomerId, result.Shipment);
    })
    .Build();

// Start the stream
orderStream.Start();

// Emit orders to the left stream
orderStream.Emit(new Order("ORD-001", 100, 150.00m, DateTime.UtcNow));
orderStream.Emit(new Order("ORD-002", 101, 75.50m, DateTime.UtcNow));

// Feed shipments to the right stream (from another source)
// This could come from a message queue, webhook, etc.
var shipment1 = new Shipment("SHP-001", "ORD-001", "FedEx", DateTime.UtcNow);
joinOperator.ProcessRight(shipment1);  // Matches with ORD-001!

var shipment2 = new Shipment("SHP-002", "ORD-002", "UPS", DateTime.UtcNow);
joinOperator.ProcessRight(shipment2);  // Matches with ORD-002!
```

**Output:**
```
Order ORD-001 shipped via FedEx!
Order ORD-002 shipped via UPS!
```

---

## Real-World Use Cases

### 1. E-Commerce: Order Fulfillment Tracking

Track the complete lifecycle of orders by joining multiple event streams:

```csharp
public record OrderPlaced(string OrderId, int CustomerId, List<string> Items, decimal Total, DateTime Timestamp);
public record PaymentReceived(string PaymentId, string OrderId, decimal Amount, string Method, DateTime Timestamp);
public record OrderFulfillment(
    string OrderId, 
    int CustomerId, 
    decimal Total,
    bool IsPaid,
    string? PaymentMethod,
    TimeSpan? PaymentDelay);

// Join orders with payments (payments should arrive within 30 minutes)
var orderPaymentJoin = new StreamStreamJoinOperator<OrderPlaced, PaymentReceived, string, OrderFulfillment>(
    order => order.OrderId,
    payment => payment.OrderId,
    order => order.Timestamp,
    payment => payment.Timestamp,
    (order, payment) => new OrderFulfillment(
        order.OrderId,
        order.CustomerId,
        order.Total,
        payment != null,
        payment?.Method,
        payment != null ? payment.Timestamp - order.Timestamp : null),
    new StreamJoinConfiguration
    {
        WindowSize = TimeSpan.FromMinutes(30),
        JoinType = StreamJoinType.Left,  // Emit orders even without payment (for follow-up)
        GracePeriod = TimeSpan.FromMinutes(5)  // Allow slightly late payments
    });

var fulfillmentStream = StreamBuilder<OrderPlaced>.CreateNewStream("OrderFulfillment")
    .Stream()
    .JoinStream(orderPaymentJoin)
    .Sink(fulfillment =>
    {
        if (fulfillment.IsPaid)
        {
            Console.WriteLine($"? Order {fulfillment.OrderId} paid via {fulfillment.PaymentMethod} " +
                            $"(delay: {fulfillment.PaymentDelay?.TotalSeconds:F0}s)");
            StartShipmentProcess(fulfillment);
        }
        else
        {
            Console.WriteLine($"?? Order {fulfillment.OrderId} unpaid - sending reminder to customer {fulfillment.CustomerId}");
            SendPaymentReminder(fulfillment.CustomerId, fulfillment.OrderId);
        }
    })
    .Build();

fulfillmentStream.Start();

// Simulate order and payment events
fulfillmentStream.Emit(new OrderPlaced("ORD-100", 1, new() { "SKU-A", "SKU-B" }, 99.99m, DateTime.UtcNow));

// Payment arrives 5 seconds later
await Task.Delay(5000);
orderPaymentJoin.ProcessRight(new PaymentReceived("PAY-100", "ORD-100", 99.99m, "Credit Card", DateTime.UtcNow));
```

---

### 2. Ride-Sharing: Matching Ride Requests with Driver Assignments

```csharp
public record RideRequest(string RequestId, string UserId, Location Pickup, Location Dropoff, DateTime Timestamp);
public record DriverAssignment(string AssignmentId, string RequestId, string DriverId, string VehicleInfo, DateTime Timestamp);
public record RideMatch(
    string RequestId,
    string UserId,
    Location Pickup,
    string? DriverId,
    string? VehicleInfo,
    bool IsMatched,
    TimeSpan? WaitTime);

var rideMatchJoin = new StreamStreamJoinOperator<RideRequest, DriverAssignment, string, RideMatch>(
    request => request.RequestId,
    assignment => assignment.RequestId,
    request => request.Timestamp,
    assignment => assignment.Timestamp,
    (request, assignment) => new RideMatch(
        request.RequestId,
        request.UserId,
        request.Pickup,
        assignment?.DriverId,
        assignment?.VehicleInfo,
        assignment != null,
        assignment != null ? assignment.Timestamp - request.Timestamp : null),
    new StreamJoinConfiguration
    {
        WindowSize = TimeSpan.FromMinutes(10),  // Max wait time for driver
        JoinType = StreamJoinType.Left,
        CleanupInterval = TimeSpan.FromSeconds(30)
    });

var rideStream = StreamBuilder<RideRequest>.CreateNewStream("RideMatching")
    .Stream()
    .JoinStream(rideMatchJoin)
    .Sink(match =>
    {
        if (match.IsMatched)
        {
            Console.WriteLine($"?? Ride {match.RequestId}: Driver {match.DriverId} assigned " +
                            $"(wait: {match.WaitTime?.TotalMinutes:F1} min)");
            NotifyRider(match.UserId, match.DriverId, match.VehicleInfo);
        }
        else
        {
            Console.WriteLine($"?? Ride {match.RequestId}: No driver found after window expired");
            OfferAlternatives(match.UserId, match.Pickup);
        }
    })
    .Build();

// Integration with external systems
rideStream.Start();

// Ride requests come from mobile app
mobileAppQueue.Subscribe(request => rideStream.Emit(request));

// Driver assignments come from dispatch system
dispatchQueue.Subscribe(assignment => rideMatchJoin.ProcessRight(assignment));
```

---

### 3. Web Analytics: Click Attribution

Join ad impressions with clicks to calculate click-through rates:

```csharp
public record AdImpression(string ImpressionId, string CampaignId, string UserId, string AdUnit, DateTime Timestamp);
public record AdClick(string ClickId, string ImpressionId, string LandingPage, DateTime Timestamp);
public record AttributedClick(
    string CampaignId,
    string AdUnit,
    string UserId,
    bool Clicked,
    TimeSpan? TimeToClick,
    string? LandingPage);

var clickAttributionJoin = new StreamStreamJoinOperator<AdImpression, AdClick, string, AttributedClick>(
    impression => impression.ImpressionId,
    click => click.ImpressionId,
    impression => impression.Timestamp,
    click => click.Timestamp,
    (impression, click) => new AttributedClick(
        impression.CampaignId,
        impression.AdUnit,
        impression.UserId,
        click != null,
        click != null ? click.Timestamp - impression.Timestamp : null,
        click?.LandingPage),
    new StreamJoinConfiguration
    {
        WindowSize = TimeSpan.FromMinutes(30),  // Attribution window
        JoinType = StreamJoinType.Outer,  // Track both clicked and unclicked impressions
        GracePeriod = TimeSpan.FromMinutes(5)
    });

var analyticsStream = StreamBuilder<AdImpression>.CreateNewStream("ClickAttribution")
    .Stream()
    .JoinStream(clickAttributionJoin)
    .Sink(attribution =>
    {
        // Update campaign metrics
        UpdateCampaignMetrics(attribution.CampaignId, attribution.AdUnit, attribution.Clicked);
        
        if (attribution.Clicked)
        {
            Console.WriteLine($"?? Campaign {attribution.CampaignId}: Click on {attribution.AdUnit} " +
                            $"after {attribution.TimeToClick?.TotalSeconds:F1}s ? {attribution.LandingPage}");
        }
    })
    .Build();
```

---

### 4. IoT: Multi-Sensor Data Fusion

Combine readings from temperature and humidity sensors for HVAC control:

```csharp
public record TemperatureReading(string RoomId, double Celsius, DateTime Timestamp);
public record HumidityReading(string RoomId, double Percentage, DateTime Timestamp);
public record RoomClimate(
    string RoomId,
    double? Temperature,
    double? Humidity,
    double? HeatIndex,
    string ComfortLevel,
    DateTime Timestamp);

// Calculate heat index when both readings are available
double? CalculateHeatIndex(double? temp, double? humidity)
{
    if (temp == null || humidity == null) return null;
    // Simplified heat index formula
    return temp.Value + (0.5 * humidity.Value);
}

string DetermineComfortLevel(double? temp, double? humidity)
{
    if (temp == null || humidity == null) return "Unknown";
    if (temp < 18) return "Too Cold";
    if (temp > 26) return "Too Hot";
    if (humidity < 30) return "Too Dry";
    if (humidity > 60) return "Too Humid";
    return "Comfortable";
}

var climateFusionJoin = new StreamStreamJoinOperator<TemperatureReading, HumidityReading, string, RoomClimate>(
    temp => temp.RoomId,
    humidity => humidity.RoomId,
    temp => temp.Timestamp,
    humidity => humidity.Timestamp,
    (temp, humidity) => new RoomClimate(
        temp?.RoomId ?? humidity!.RoomId,
        temp?.Celsius,
        humidity?.Percentage,
        CalculateHeatIndex(temp?.Celsius, humidity?.Percentage),
        DetermineComfortLevel(temp?.Celsius, humidity?.Percentage),
        DateTime.UtcNow),
    new StreamJoinConfiguration
    {
        WindowSize = TimeSpan.FromMinutes(5),  // Readings should arrive within 5 min of each other
        JoinType = StreamJoinType.Outer,  // Process readings even if one sensor fails
        CleanupInterval = TimeSpan.FromMinutes(1)
    });

var hvacStream = StreamBuilder<TemperatureReading>.CreateNewStream("ClimateControl")
    .Stream()
    .JoinStream(climateFusionJoin)
    .Filter(climate => climate.ComfortLevel != "Comfortable")
    .Sink(climate =>
    {
        Console.WriteLine($"??? Room {climate.RoomId}: {climate.ComfortLevel} " +
                        $"(Temp: {climate.Temperature}°C, Humidity: {climate.Humidity}%)");
        AdjustHVAC(climate.RoomId, climate.ComfortLevel);
    })
    .Build();

hvacStream.Start();

// Temperature sensors
tempSensorMqtt.Subscribe(reading => hvacStream.Emit(reading));

// Humidity sensors (different MQTT topic)
humiditySensorMqtt.Subscribe(reading => climateFusionJoin.ProcessRight(reading));
```

---

### 5. Financial: Trade Execution Matching

Match trade orders with their executions for compliance reporting:

```csharp
public record TradeOrder(string OrderId, string Symbol, int Quantity, decimal Price, string Side, DateTime Timestamp);
public record TradeExecution(string ExecutionId, string OrderId, int FilledQty, decimal AvgPrice, DateTime Timestamp);
public record TradeReport(
    string OrderId,
    string Symbol,
    string Side,
    int OrderedQty,
    int? FilledQty,
    decimal OrderPrice,
    decimal? ExecutionPrice,
    decimal? Slippage,
    TimeSpan? ExecutionTime,
    string Status);

var tradeMatchJoin = new StreamStreamJoinOperator<TradeOrder, TradeExecution, string, TradeReport>(
    order => order.OrderId,
    execution => execution.OrderId,
    order => order.Timestamp,
    execution => execution.Timestamp,
    (order, execution) => new TradeReport(
        order.OrderId,
        order.Symbol,
        order.Side,
        order.Quantity,
        execution?.FilledQty,
        order.Price,
        execution?.AvgPrice,
        execution != null ? Math.Abs(execution.AvgPrice - order.Price) : null,
        execution != null ? execution.Timestamp - order.Timestamp : null,
        execution != null 
            ? (execution.FilledQty == order.Quantity ? "Filled" : "Partial") 
            : "Pending"),
    new StreamJoinConfiguration
    {
        WindowSize = TimeSpan.FromMinutes(15),  // Orders should execute within 15 min
        JoinType = StreamJoinType.Left,  // Track all orders even if not executed
        GracePeriod = TimeSpan.FromSeconds(30)
    });

var complianceStream = StreamBuilder<TradeOrder>.CreateNewStream("TradeCompliance")
    .Stream()
    .JoinStream(tradeMatchJoin)
    .Sink(report =>
    {
        // Log for compliance
        LogTradeReport(report);
        
        if (report.Status == "Pending" && report.ExecutionTime == null)
        {
            Console.WriteLine($"?? Order {report.OrderId} ({report.Symbol}) not executed - escalating");
            EscalateUnexecutedOrder(report);
        }
        else if (report.Slippage > 0.05m)  // More than 5 cents slippage
        {
            Console.WriteLine($"?? Order {report.OrderId}: High slippage detected (${report.Slippage})");
            FlagForReview(report);
        }
    })
    .Build();
```

---

## Configuration Options

### StreamJoinConfiguration

```csharp
var config = new StreamJoinConfiguration
{
    // How long to buffer events for potential matches
    WindowSize = TimeSpan.FromMinutes(10),
    
    // Join semantics
    JoinType = StreamJoinType.Inner,  // Inner, Left, Right, or Outer
    
    // How often to clean up expired events
    CleanupInterval = TimeSpan.FromSeconds(30),
    
    // Extra time to wait for late events before emitting unmatched
    GracePeriod = TimeSpan.FromSeconds(10),
    
    // Prevent memory issues with high-cardinality keys
    MaxBufferSizePerKey = 1000
};
```

### Factory Methods

```csharp
// Quick configurations
var innerConfig = StreamJoinConfiguration.InnerJoin(TimeSpan.FromMinutes(5));
var leftConfig = StreamJoinConfiguration.LeftJoin(TimeSpan.FromMinutes(5));
var outerConfig = StreamJoinConfiguration.OuterJoin(TimeSpan.FromMinutes(5));
```

---

## Architecture Patterns

### Pattern 1: Dual Stream Sources

```csharp
// Both streams from message queues
var joinOp = new StreamStreamJoinOperator<OrderEvent, PaymentEvent, string, Result>(...);

var orderStream = StreamBuilder<OrderEvent>.CreateNewStream("Orders")
    .Stream(new KafkaSourceOperator<OrderEvent>("orders-topic"))
    .JoinStream(joinOp)
    .Sink(Process)
    .Build();

// Separate consumer for payments
var paymentConsumer = new KafkaConsumer<PaymentEvent>("payments-topic");
paymentConsumer.Subscribe(payment => joinOp.ProcessRight(payment));

orderStream.Start();
paymentConsumer.Start();
```

### Pattern 2: HTTP Webhook Integration

```csharp
var joinOp = new StreamStreamJoinOperator<InternalEvent, WebhookEvent, string, Result>(...);

// Internal stream
var internalStream = StreamBuilder<InternalEvent>.CreateNewStream("Internal")
    .Stream()
    .JoinStream(joinOp)
    .Sink(Process)
    .Build();

// ASP.NET Core webhook endpoint
app.MapPost("/webhook", (WebhookEvent evt) => 
{
    joinOp.ProcessRight(evt);
    return Results.Ok();
});
```

---

## Best Practices

1. **Choose appropriate window sizes:**
   - Too small: miss legitimate matches
   - Too large: high memory usage
   - Consider your SLAs and typical latencies

2. **Handle late-arriving data:**
   ```csharp
   GracePeriod = TimeSpan.FromMinutes(2)  // Allow 2 min for late events
   ```

3. **Monitor buffer sizes:**
   ```csharp
   // Periodically check
   Console.WriteLine($"Left buffer: {joinOp.GetLeftBufferCount()}, Right buffer: {joinOp.GetRightBufferCount()}");
   ```

4. **Use appropriate join types:**
   - `Inner`: Only care about matched pairs
   - `Left`: Must process all left events
   - `Outer`: Need complete visibility of both streams

5. **Dispose when done:**
   ```csharp
   // Stops the cleanup timer
   joinOp.Dispose();
   ```

6. **Handle null gracefully in join functions:**
   ```csharp
   (left, right) => new Result(
       left?.Id ?? right!.RefId,  // One side might be null in outer joins
       left?.Value,
       right?.Value)
   ```

---

## Memory Considerations

| Factor | Impact | Mitigation |
|--------|--------|------------|
| Window Size | Larger = more memory | Use smallest window that meets requirements |
| Event Rate | Higher = more memory | Consider sampling or pre-aggregation |
| Key Cardinality | More keys = more memory | Use `MaxBufferSizePerKey` |
| Event Size | Larger events = more memory | Store only needed fields |

```csharp
// Memory-conscious configuration
var config = new StreamJoinConfiguration
{
    WindowSize = TimeSpan.FromMinutes(5),
    MaxBufferSizePerKey = 100,  // Limit per key
    CleanupInterval = TimeSpan.FromSeconds(10)  // Frequent cleanup
};
```

---

## See Also

- [Stream-Table Joins](./Stream-Table-Joins.md) - For joining streams with reference tables
- [Windowing Operations](./Windowing.md) - Time-based aggregations
- [State Stores](./State-Stores.md) - Persistent state backends
