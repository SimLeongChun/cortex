# Unified Error Handling for All Stream Integrations

## Summary

This issue documents the implementation of unified, stream-level error handling across **all** stream integration sink operators, replacing the previous per-operator error handling approach.

## Problem Statement

Previously, each integration (messaging, storage, databases, HTTP) implemented its own error handling with custom parameters:

```csharp
// OLD: Each operator had its own error handling parameters
new KafkaSinkOperator<Order>(
    bootstrapServers: "localhost:9092",
    topic: "orders",
    maxRetries: 3,                              // ? Duplicated across integrations
    retryDelayMs: 100,                          // ? Inconsistent behavior
    errorHandler: (ex, msg) => { ... }          // ? Per-operator configuration
);

// OLD: HTTP had its own retry logic
new HttpSinkOperator<Order>(
    endpoint: "https://api.example.com/orders",
    maxRetries: 3,
    initialDelay: TimeSpan.FromMilliseconds(500)
);

// OLD: Azure Blob Storage used Polly
new AzureBlobStorageSinkOperator<Order>(
    connectionString: "...",
    containerName: "orders",
    directoryPath: "data"
    // Internal Polly retry policy
);
```

### Issues with the Previous Approach

1. **Code Duplication**: Each integration had its own retry/error logic
2. **Inconsistent Behavior**: Different integrations might handle errors differently
3. **Configuration Complexity**: Error handling configured per-operator, not centrally
4. **No Integration with Core**: Didn't leverage the existing `StreamExecutionOptions` infrastructure
5. **Mixed Patterns**: Some used Polly, some used manual loops, some had callbacks

## Solution

### 1. Core Library Changes

Made the error handling infrastructure public so external integrations can use it:

**`Cortex.Streams/ErrorHandling/ErrorHandlingHelper.cs`**
```csharp
// Changed from internal to public
public static class ErrorHandlingHelper
{
    public static bool TryExecute<TInput>(
        StreamExecutionOptions options,
        string operatorName,
        object rawInput,
        Action<TInput> action) { ... }
}
```

**`Cortex.Streams/ErrorHandling/StreamExecutionOptions.cs`**
```csharp
// Made Default public
public static readonly StreamExecutionOptions Default = new StreamExecutionOptions();
```

### 2. Operator Adapters & FanOut Support

Fixed critical bug where `StreamExecutionOptions` were not being forwarded to integration sink operators:

**`SinkOperatorAdapter<T>`** - Now implements `IErrorHandlingEnabled` and forwards to wrapped operator
**`BranchOperator<T>`** - Now implements `IErrorHandlingEnabled` and forwards to branch operators
**`ForkOperator<T>`** - Now implements `IErrorHandlingEnabled` and forwards to all branches

### 3. All Integration Sink Operators Updated

All sink operators now implement `IErrorHandlingEnabled`:

#### Messaging Integrations
| Operator | Package |
|----------|---------|
| `KafkaSinkOperator<TInput>` | Cortex.Streams.Kafka |
| `KafkaKeyValueSinkOperator<TKey, TValue>` | Cortex.Streams.Kafka |
| `PulsarSinkOperator<TInput>` | Cortex.Streams.Pulsar |
| `RabbitMQSinkOperator<TInput>` | Cortex.Streams.RabbitMQ |
| `SQSSinkOperator<TInput>` | Cortex.Streams.AWSSQS |
| `AzureServiceBusSinkOperator<TInput>` | Cortex.Streams.AzureServiceBus |

#### Storage Integrations
| Operator | Package |
|----------|---------|
| `S3SinkOperator<TInput>` | Cortex.Streams.S3 |
| `S3SinkBulkOperator<TInput>` | Cortex.Streams.S3 |
| `AzureBlobStorageSinkOperator<TInput>` | Cortex.Streams.AzureBlobStorage |
| `AzureBlobStorageBulkSinkOperator<TInput>` | Cortex.Streams.AzureBlobStorage |
| `FileSinkOperator<TInput>` | Cortex.Streams.Files |

#### Database Integrations
| Operator | Package |
|----------|---------|
| `ElasticsearchSinkOperator<TInput>` | Cortex.Streams.Elasticsearch |

#### HTTP Integrations
| Operator | Package |
|----------|---------|
| `HttpSinkOperator<TInput>` | Cortex.Streams.Http |
| `HttpSinkOperatorAsync<TInput>` | Cortex.Streams.Http |

#### Mediator Integrations
| Operator | Package |
|----------|---------|
| `MediatorCommandSinkOperator<TInput, TCommand, TResult>` | Cortex.Streams.Mediator |
| `MediatorVoidCommandSinkOperator<TInput, TCommand>` | Cortex.Streams.Mediator |
| `MediatorNotificationSinkOperator<TInput, TNotification>` | Cortex.Streams.Mediator |
| `MediatorDirectNotificationSinkOperator<TNotification>` | Cortex.Streams.Mediator |

**New Pattern (consistent across all operators):**
```csharp
public class KafkaSinkOperator<TInput> : ISinkOperator<TInput>, IErrorHandlingEnabled, IDisposable
{
    private static readonly string OperatorName = $"KafkaSinkOperator<{typeof(TInput).Name}>";
    private StreamExecutionOptions _executionOptions = StreamExecutionOptions.Default;

    public void SetErrorHandling(StreamExecutionOptions options)
    {
        _executionOptions = options ?? StreamExecutionOptions.Default;
    }

    public void Process(TInput input)
    {
        // Use core error handling - consistent across ALL integrations
        ErrorHandlingHelper.TryExecute(
            _executionOptions,
            OperatorName,
            input,
            (Action<TInput>)ProduceMessage);
    }
}
```

## Usage

### Simple Stream with Error Handling

```csharp
var stream = StreamBuilder<Order, Order>
    .CreateNewStream("order-processor")
    .WithExecutionOptions(new StreamExecutionOptions
    {
        ErrorHandlingStrategy = ErrorHandlingStrategy.Retry,
        MaxRetries = 5,
        RetryDelay = TimeSpan.FromSeconds(1)
    })
    .Stream(sourceOperator)
    .Map(order => ProcessOrder(order))
    .Sink(new KafkaSinkOperator<Order>("localhost:9092", "orders"))
    .Build();
```

### Multi-Destination with Unified Error Handling

```csharp
var stream = StreamBuilder<Order, Order>
    .CreateNewStream("order-fanout")
    .WithExecutionOptions(new StreamExecutionOptions
    {
        ErrorHandlingStrategy = ErrorHandlingStrategy.Skip,
        OnError = ctx => 
        {
            logger.LogError(ctx.Exception, 
                "Error in {Operator} processing {Input}", 
                ctx.OperatorName, ctx.Input);
            return ErrorHandlingDecision.Skip;
        }
    })
    .Stream(sourceOperator)
    .FanOut()
        .To("kafka", new KafkaSinkOperator<Order>("kafka:9092", "orders"))
        .To("s3", new S3SinkOperator<Order>("my-bucket", "orders", s3Client))
        .To("elasticsearch", new ElasticsearchSinkOperator<Order>(esClient, "orders-index"))
        .To("http", new HttpSinkOperator<Order>("https://api.example.com/orders"))
    .Build();
```

### Custom Per-Error Decision

```csharp
.WithExecutionOptions(new StreamExecutionOptions
{
    OnError = ctx => 
    {
        // Retry transient errors
        if (ctx.Exception is TimeoutException || 
            ctx.Exception is HttpRequestException ||
            ctx.Exception is AmazonS3Exception s3Ex && s3Ex.StatusCode == HttpStatusCode.ServiceUnavailable)
            return ErrorHandlingDecision.Retry;
        
        // Skip serialization errors
        if (ctx.Exception is JsonException)
            return ErrorHandlingDecision.Skip;
        
        // Stop on critical errors
        if (ctx.Exception is AuthenticationException)
            return ErrorHandlingDecision.Stop;
        
        // Default: rethrow
        return ErrorHandlingDecision.Rethrow;
    }
})
```

## Error Handling Flow

```
WithExecutionOptions(options)
    ?
StreamBuilder._executionOptions = options
    ?
Build() ? new Stream(..., executionOptions)
    ?
Stream.InitializeErrorHandling(_operatorChain)
    ?
Recursively traverses operator chain via IHasNextOperators
    ?
For each IErrorHandlingEnabled operator:
    ?
operator.SetErrorHandling(options)
    ?
SinkOperatorAdapter ? forwards to wrapped ISinkOperator
ForkOperator ? forwards to all BranchOperators
BranchOperator ? forwards to inner operators
    ?
All sink operators (Kafka, S3, HTTP, etc.) receive the same options
```

## Breaking Changes

### Constructor Parameter Changes

The following parameters have been **removed** from ALL integration sink operators:
- `maxRetries`
- `retryDelayMs` / `initialDelay`
- `errorHandler`
- `maxQueueSize`
- Internal Polly policies

**Migration Examples:**

```csharp
// OLD - Kafka
new KafkaSinkOperator<Order>(
    bootstrapServers: "localhost:9092",
    topic: "orders",
    maxRetries: 5,
    retryDelayMs: 1000,
    errorHandler: (ex, msg) => Console.WriteLine(ex)
);

// NEW - Kafka
.WithExecutionOptions(new StreamExecutionOptions
{
    ErrorHandlingStrategy = ErrorHandlingStrategy.Retry,
    MaxRetries = 5,
    RetryDelay = TimeSpan.FromSeconds(1)
})
.Sink(new KafkaSinkOperator<Order>("localhost:9092", "orders"))

// OLD - HTTP
new HttpSinkOperator<Order>(
    endpoint: "https://api.example.com",
    maxRetries: 3,
    initialDelay: TimeSpan.FromMilliseconds(500)
);

// NEW - HTTP
.WithExecutionOptions(new StreamExecutionOptions
{
    ErrorHandlingStrategy = ErrorHandlingStrategy.Retry,
    MaxRetries = 3,
    RetryDelay = TimeSpan.FromMilliseconds(500)
})
.Sink(new HttpSinkOperator<Order>("https://api.example.com"))

// OLD - Mediator with error handler
new MediatorCommandSinkOperator<Order, ProcessOrderCommand, OrderResult>(
    mediator,
    order => new ProcessOrderCommand(order),
    resultHandler: (o, r) => Console.WriteLine(r),
    errorHandler: (o, ex) => Console.WriteLine(ex)
);

// NEW - Mediator (error handling at stream level)
.WithExecutionOptions(new StreamExecutionOptions
{
    OnError = ctx => { Console.WriteLine(ctx.Exception); return ErrorHandlingDecision.Skip; }
})
.Sink(new MediatorCommandSinkOperator<Order, ProcessOrderCommand, OrderResult>(
    mediator,
    order => new ProcessOrderCommand(order),
    resultHandler: (o, r) => Console.WriteLine(r)
))
```

## Files Changed

### Core Library
- `src/Cortex.Streams/ErrorHandling/ErrorHandlingHelper.cs` - Made public
- `src/Cortex.Streams/ErrorHandling/StreamExecutionOptions.cs` - Made Default public
- `src/Cortex.Streams/Operators/SinkOperatorAdapter.cs` - Added IErrorHandlingEnabled
- `src/Cortex.Streams/Operators/BranchOperator.cs` - Added IErrorHandlingEnabled
- `src/Cortex.Streams/Operators/ForkOperator.cs` - Added IErrorHandlingEnabled

### Messaging Integration Libraries
- `src/Cortex.Streams.Kafka/KafkaSinkOperator.cs`
- `src/Cortex.Streams.Kafka/KafkaKeyValueSinkOperator.cs`
- `src/Cortex.Streams.Kafka/KafkaSourceOperator.cs`
- `src/Cortex.Streams.Pulsar/PulsarSinkOperator.cs`
- `src/Cortex.Streams.RabbitMQ/RabbitMQSinkOperator.cs`
- `src/Cortex.Streams.AWSSQS/SQSSinkOperator.cs`
- `src/Cortex.Streams.AzureServiceBus/AzureServiceBusSinkOperator.cs`

### Storage Integration Libraries
- `src/Cortex.Streams.S3/S3SinkOperator.cs`
- `src/Cortex.Streams.S3/S3SinkBulkOperator.cs`
- `src/Cortex.Streams.AzureBlobStorage/AzureBlobStorageSinkOperator.cs`
- `src/Cortex.Streams.AzureBlobStorage/AzureBlobStorageBulkSinkOperator.cs`
- `src/Cortex.Streams.Files/FileSinkOperator.cs`

### Database Integration Libraries
- `src/Cortex.Streams.Elasticsearch/ElasticsearchSinkOperator.cs`

### HTTP Integration Libraries
- `src/Cortex.Streams.Http/HttpSinkOperator.cs`
- `src/Cortex.Streams.Http/HttpSinkOperatorAsync.cs`

### Mediator Integration Libraries
- `src/Cortex.Streams.Mediator/Operators/MediatorCommandSinkOperator.cs`
- `src/Cortex.Streams.Mediator/Operators/MediatorNotificationSinkOperator.cs`
- `src/Cortex.Streams.Mediator/Extensions/StreamBuilderMediatorExtensions.cs`
- `src/Cortex.Streams.Mediator/DependencyInjection/ServiceCollectionExtensions.cs`

### Test Files Updated
- `src/Cortex.Tests/StreamsMediator/Tests/MediatorCommandSinkOperatorTests.cs`
- `src/Cortex.Tests/StreamsMediator/Tests/MediatorNotificationSinkOperatorTests.cs`
- `src/Cortex.Tests/StreamsMediator/Tests/StreamBuilderMediatorExtensionsTests.cs`

## Benefits

| Aspect | Before | After |
|--------|--------|-------|
| Configuration | Per-operator | Centralized at stream level |
| Consistency | Different per integration | Unified behavior across all 17+ operators |
| Code | Duplicated retry logic (Polly, manual loops, callbacks) | Single `ErrorHandlingHelper` |
| Flexibility | Fixed strategy per operator | Dynamic per-error decisions |
| Observability | Manual logging | Rich `StreamErrorContext` with operator name |
| FanOut | No support | Full support across all branches |
| Maintenance | Update each integration separately | Single point of change |

## Libraries Without Sink Operators (No Changes Needed)

The following libraries only have source operators and were not modified:
- `Cortex.Streams.MongoDb` - CDC source operators only
- `Cortex.Streams.MSSqlServer` - CDC source operators only
- `Cortex.Streams.PostgreSQL` - CDC source operators only

## Related Issues

- Relates to core error handling infrastructure in `Cortex.Streams.ErrorHandling`
- Enables consistent error handling across **all** integrations
- Supports both simple streams and complex FanOut topologies

## Labels

`enhancement` `breaking-change` `error-handling` `kafka` `pulsar` `rabbitmq` `sqs` `servicebus` `s3` `azure-blob` `elasticsearch` `http` `mediator` `files`
