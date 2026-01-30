# Cortex.Streams.Mediator

Integration library that bridges **Cortex.Streams** with **Cortex.Mediator**, enabling seamless CQRS pattern integration with stream processing pipelines.

## Overview

This package provides bidirectional integration between Cortex's real-time stream processing and the Mediator pattern (CQRS), allowing you to:

- **Sink stream data to Commands**: Route stream events to CQRS command handlers
- **Publish stream data as Notifications**: Broadcast stream events to multiple notification handlers
- **Source streams from Streaming Queries**: Use Mediator's streaming queries as data sources for streams
- **Enrich stream data with Queries**: Transform stream data by executing queries
- **Route Mediator events to Streams**: Emit commands, queries, and notifications to streams for processing

## Installation

```bash
dotnet add package Cortex.Streams.Mediator
```

## Quick Start

### 1. Sink Stream Data to Commands

Route stream events through CQRS command handlers:

```csharp
// Define your command
public class ProcessOrderCommand : ICommand<OrderResult>
{
    public string OrderId { get; set; }
    public decimal Amount { get; set; }
}

// Build the stream with command sink
var stream = StreamBuilder<OrderEvent, OrderEvent>
    .CreateNewStream("OrderProcessingStream")
    .Stream()
    .Filter(e => e.Status == "Pending")
    .SinkToCommand<OrderEvent, OrderEvent, ProcessOrderCommand, OrderResult>(
        mediator,
        orderEvent => new ProcessOrderCommand 
        { 
            OrderId = orderEvent.Id, 
            Amount = orderEvent.TotalAmount 
        },
        resultHandler: (order, result) => Console.WriteLine($"Order {order.Id} processed: {result.Status}"),
        errorHandler: (order, ex) => Console.WriteLine($"Failed to process order {order.Id}: {ex.Message}"))
    .Build();

stream.Start();
```

### 2. Publish Stream Events as Notifications

Broadcast stream events to multiple handlers:

```csharp
// Define your notification
public class OrderProcessedNotification : INotification
{
    public string OrderId { get; set; }
    public DateTime ProcessedAt { get; set; }
}

// Build the stream with notification sink
var stream = StreamBuilder<OrderEvent, OrderEvent>
    .CreateNewStream("OrderNotificationStream")
    .Stream()
    .Filter(e => e.Status == "Completed")
    .SinkToNotification<OrderEvent, OrderEvent, OrderProcessedNotification>(
        mediator,
        orderEvent => new OrderProcessedNotification 
        { 
            OrderId = orderEvent.Id, 
            ProcessedAt = DateTime.UtcNow 
        })
    .Build();

stream.Start();
```

### 3. Source Streams from Mediator Streaming Queries

Use Mediator's streaming queries as data sources:

```csharp
// Define your streaming query
public class GetLiveOrdersQuery : IStreamQuery<OrderEvent>
{
    public string Region { get; set; }
}

// Build the stream sourced from mediator
var stream = StreamBuilder<OrderEvent, OrderEvent>
    .CreateNewStream("LiveOrdersStream")
    .StreamFromQuery<OrderEvent, OrderEvent, GetLiveOrdersQuery>(
        mediator,
        new GetLiveOrdersQuery { Region = "US" },
        errorHandler: ex => Console.WriteLine($"Query error: {ex.Message}"))
    .Filter(e => e.Amount > 100)
    .Sink(e => Console.WriteLine($"High-value order: {e.Id}"))
    .Build();

stream.Start();
```

### 4. Route Notifications to Streams

Handle Mediator notifications by emitting to streams:

```csharp
// In your DI configuration
services.AddStreamEmittingNotificationHandler<OrderCreatedNotification>(
    sp => sp.GetRequiredService<IStream<OrderCreatedNotification, OrderCreatedNotification>>(),
    errorHandler: (notification, ex) => logger.LogError(ex, "Failed to emit notification"));
```

### 5. Command Pipeline with Stream Auditing

Emit command execution events to streams for auditing:

```csharp
// Register the behavior in DI
services.AddTransient<ICommandPipelineBehavior<ProcessOrderCommand, OrderResult>>(sp =>
    new StreamEmittingCommandBehavior<ProcessOrderCommand, OrderResult>(
        sp.GetRequiredService<IStream<CommandExecutionEvent<ProcessOrderCommand, OrderResult>, ...>>(),
        emitBeforeExecution: true,
        emitAfterExecution: true));
```

## Available Components

### Sink Operators

| Operator | Description |
|----------|-------------|
| `MediatorCommandSinkOperator<TInput, TCommand, TResult>` | Dispatches stream data as commands with results |
| `MediatorVoidCommandSinkOperator<TInput, TCommand>` | Dispatches stream data as void commands |
| `MediatorNotificationSinkOperator<TInput, TNotification>` | Publishes stream data as notifications |
| `MediatorDirectNotificationSinkOperator<TNotification>` | Publishes notifications directly when input implements INotification |

### Source Operators

| Operator | Description |
|----------|-------------|
| `MediatorStreamQuerySourceOperator<TQuery, TOutput>` | Sources stream data from a streaming query |
| `MediatorStreamQueryFactorySourceOperator<TQuery, TOutput>` | Sources with lazy query creation |

### Map/Transform Operators

| Operator | Description |
|----------|-------------|
| `MediatorQueryMapOperator<TInput, TQuery, TOutput>` | Transforms data using query results |
| `MediatorQueryEnrichOperator<TInput, TQuery, TQueryResult, TOutput>` | Enriches data with query results |

### Filter Operators

| Operator | Description |
|----------|-------------|
| `MediatorCommandFilterOperator<TInput, TCommand, TResult>` | Filters based on command execution results |

### Pipeline Behaviors

| Behavior | Description |
|----------|-------------|
| `StreamEmittingCommandBehavior<TCommand, TResult>` | Emits command execution events to streams |
| `StreamEmittingNotificationBehavior<TNotification>` | Emits notification handling events to streams |

### Handler Base Classes

| Handler | Description |
|---------|-------------|
| `StreamEmittingCommandHandler<TCommand, TResult>` | Command handler that emits results to streams |
| `StreamEmittingVoidCommandHandler<TCommand>` | Void command handler that emits commands to streams |
| `StreamEmittingNotificationHandler<TNotification>` | Notification handler that emits to streams |
| `StreamBackedStreamQueryHandler<TQuery, TResult>` | Streaming query backed by a Cortex Stream |

## Extension Methods

### For IStreamBuilder

```csharp
// Sink to command with result
builder.SinkToCommand<TIn, TCurrent, TCommand, TResult>(mediator, commandFactory, resultHandler, errorHandler);

// Sink to void command
builder.SinkToVoidCommand<TIn, TCurrent, TCommand>(mediator, commandFactory, completionHandler, errorHandler);

// Sink to notification
builder.SinkToNotification<TIn, TCurrent, TNotification>(mediator, notificationFactory, completionHandler, errorHandler);

// Publish notification directly
builder.PublishNotification<TIn, TNotification>(mediator, completionHandler, errorHandler);
```

### For IInitialStreamBuilder

```csharp
// Source from streaming query
builder.StreamFromQuery<TIn, TCurrent, TQuery>(mediator, query, errorHandler);

// Source from query factory
builder.StreamFromQueryFactory<TIn, TCurrent, TQuery>(mediator, queryFactory, errorHandler);
```

## Dependency Injection

```csharp
services.AddCortexMediator(new[] { typeof(Program) }, options => { ... });

services.AddCortexStreamsMediatorIntegration();

// Register notification handler that emits to stream
services.AddStreamEmittingNotificationHandler<OrderCreatedNotification>(
    sp => sp.GetRequiredService<IStream<OrderCreatedNotification, OrderCreatedNotification>>());

// Register transforming notification handler
services.AddTransformingStreamNotificationHandler<OrderCreatedNotification, OrderEvent>(
    sp => sp.GetRequiredService<IStream<OrderEvent, OrderEvent>>(),
    notification => new OrderEvent { Id = notification.OrderId });
```

## Use Cases

### Event Sourcing
Stream all commands through a command behavior to an event store:

```csharp
var eventStream = StreamBuilder<CommandExecutionEvent<CreateOrderCommand, OrderId>, ...>
    .CreateNewStream("EventStore")
    .Stream()
    .Sink(new EventStoreOperator(connectionString))
    .Build();
```

### Real-Time Analytics
Combine stream processing with CQRS queries for enrichment:

```csharp
var analyticsStream = StreamBuilder<SensorReading, SensorReading>
    .CreateNewStream("Analytics")
    .Stream(kafkaSource)
    .Map(reading => /* transform */)
    .SinkToCommand<..., AnalyzeSensorDataCommand, AnalysisResult>(mediator, ...)
    .Build();
```

### Distributed Notifications
Broadcast stream events to multiple microservices:

```csharp
var broadcastStream = StreamBuilder<DomainEvent, DomainEvent>
    .CreateNewStream("Broadcast")
    .Stream()
    .SinkToNotification<..., DomainEventNotification>(mediator, ...)
    .Build();
```

## Requirements

- .NET 6.0 or later
- Cortex.Streams
- Cortex.Mediator

## License

MIT License - see the main Cortex repository for details.
