# Cortex.Mediator.Behaviors.Transactional ??

**Cortex.Mediator.Behaviors.Transactional** provides transactional pipeline behaviors for Cortex.Mediator, enabling automatic transaction management for command execution with commit on success and rollback on failure.

Built as part of the [Cortex Data Framework](https://github.com/buildersoftio/cortex), this library ensures data consistency by wrapping command handlers in transactions.

- ? Automatic Transaction Management
- ? Async/Await Support with TransactionScope
- ? Custom Transaction Contexts (EF Core, Dapper, etc.)
- ? Configurable Isolation Levels & Timeouts
- ? Selective Command Exclusion

---

[![GitHub License](https://img.shields.io/github/license/buildersoftio/cortex)](https://github.com/buildersoftio/cortex/blob/master/LICENSE)
[![NuGet Version](https://img.shields.io/nuget/v/Cortex.Mediator.Behaviors.Transactional?label=Cortex.Mediator.Behaviors.Transactional)](https://www.nuget.org/packages/Cortex.Mediator.Behaviors.Transactional)
[![GitHub contributors](https://img.shields.io/github/contributors/buildersoftio/cortex)](https://github.com/buildersoftio/cortex)
[![Discord Shield](https://discord.com/api/guilds/1310034212371566612/widget.png?style=shield)](https://discord.gg/JnMJV33QHu)

## ?? Getting Started

### Install via NuGet

```bash
dotnet add package Cortex.Mediator.Behaviors.Transactional
```

## ??? Setup

In `Program.cs` or `Startup.cs`:

```csharp
using Cortex.Mediator.DependencyInjection;
using Cortex.Mediator.Behaviors.Transactional.DependencyInjection;

// Add mediator with transactional behaviors
builder.Services.AddCortexMediator(
    new[] { typeof(Program) },
    options => options.AddTransactionalBehaviors()
);

// Register transactional options (with defaults)
builder.Services.AddTransactionalBehavior();
```

### With Custom Options

```csharp
builder.Services.AddTransactionalBehavior(options =>
{
    options.IsolationLevel = IsolationLevel.Serializable;
    options.Timeout = TimeSpan.FromMinutes(2);
    options.ScopeOption = TransactionScopeOption.RequiresNew;
});
```

## ?? How It Works

Once configured, all commands automatically execute within a transaction:

```csharp
public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, OrderResult>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;

    public async Task<OrderResult> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        // All operations are wrapped in a transaction
        var order = await _orderRepository.CreateAsync(command);
        await _inventoryService.ReserveItemsAsync(command.Items);
        
        // ? Auto-commit on success
        // ? Auto-rollback if any exception is thrown
        return new OrderResult { OrderId = order.Id };
    }
}
```

## ?? Excluding Commands from Transactions

### Using the `[NonTransactional]` Attribute

```csharp
[NonTransactional]
public class GetProductsQuery : ICommand<IEnumerable<Product>>
{
    public string SearchTerm { get; set; }
}
```

### Using Configuration

```csharp
builder.Services.AddTransactionalBehavior(options =>
{
    // Exclude specific command types
    options.ExcludeCommand<ReadOnlyQuery>();
    
    // Or exclude multiple at once
    options.ExcludeCommands(
        typeof(GetProductsQuery),
        typeof(CacheRefreshCommand),
        typeof(LoggingCommand)
    );
});
```

## ?? Custom Transaction Context

For more control over transaction management (e.g., with Entity Framework Core):

### 1. Implement `ITransactionalContext`

```csharp
public class EfCoreTransactionalContext : ITransactionalContext
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction _transaction;

    public EfCoreTransactionalContext(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
        await _transaction.CommitAsync(ct);
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        await _transaction.RollbackAsync(ct);
    }
}
```

### 2. Register the Custom Context

```csharp
builder.Services.AddTransactionalBehavior();
builder.Services.AddTransactionalContext<EfCoreTransactionalContext>();
```

## ?? Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `IsolationLevel` | `ReadCommitted` | Transaction isolation level |
| `Timeout` | `30 seconds` | Transaction timeout duration |
| `ScopeOption` | `Required` | Transaction scope behavior |
| `AsyncFlowOption` | `Enabled` | Enables async flow for TransactionScope |
| `ExcludedCommandTypes` | `Empty` | Commands to exclude from transactions |

### Isolation Levels

```csharp
options.IsolationLevel = IsolationLevel.ReadCommitted;     // Default - good for most scenarios
options.IsolationLevel = IsolationLevel.Serializable;      // Strictest - for financial transactions
options.IsolationLevel = IsolationLevel.ReadUncommitted;   // Fastest - allows dirty reads
options.IsolationLevel = IsolationLevel.RepeatableRead;    // Prevents non-repeatable reads
options.IsolationLevel = IsolationLevel.Snapshot;          // Optimistic concurrency
```

### Transaction Scope Options

```csharp
options.ScopeOption = TransactionScopeOption.Required;     // Join existing or create new (default)
options.ScopeOption = TransactionScopeOption.RequiresNew;  // Always create a new transaction
options.ScopeOption = TransactionScopeOption.Suppress;     // Execute without a transaction
```

## ?? Pipeline Behavior Order

When using multiple pipeline behaviors, consider the registration order:

```csharp
builder.Services.AddCortexMediator(
    new[] { typeof(Program) },
    options =>
    {
        // 1. Validation first (fail fast before transaction starts)
        options.AddFluentValidationBehaviors();
        
        // 2. Transaction wraps the actual execution
        options.AddTransactionalBehaviors();
        
        // 3. Logging (optional)
        options.AddDefaultBehaviors();
    }
);
```

## ?? Best Practices

### ? Keep Transactions Short

```csharp
// Good: Only database operations
public async Task<Result> Handle(Command command, CancellationToken ct)
{
    await _repository.SaveAsync(entity);
    return Result.Success();
}

// Avoid: External calls inside transactions
public async Task<Result> Handle(Command command, CancellationToken ct)
{
    await _repository.SaveAsync(entity);
    await _emailService.SendAsync(email);  // ? External service call
    return Result.Success();
}
```

### ? Exclude Read-Only Operations

```csharp
[NonTransactional]
public class GetUserByIdQuery : ICommand<UserDto>
{
    public int UserId { get; set; }
}
```

### ? Use Appropriate Isolation Levels

```csharp
// High-throughput reads
options.IsolationLevel = IsolationLevel.ReadCommitted;

// Financial transactions
options.IsolationLevel = IsolationLevel.Serializable;
```

### ? Set Appropriate Timeouts

```csharp
// Quick operations
options.Timeout = TimeSpan.FromSeconds(15);

// Complex batch operations
options.Timeout = TimeSpan.FromMinutes(5);
```

## ?? Documentation

For complete documentation, see the [WIKI.md](./WIKI.md) file.

## ?? Contributing

We welcome contributions! See the main [Cortex repository](https://github.com/buildersoftio/cortex) for contribution guidelines.

## ?? License

This project is licensed under the MIT License.

## ?? Contact

- Email: cortex@buildersoft.io
- Website: https://buildersoft.io
- GitHub Issues: [Cortex Data Framework Issues](https://github.com/buildersoftio/cortex/issues)
- Discord: [![Discord Shield](https://discord.com/api/guilds/1310034212371566612/widget.png?style=shield)](https://discord.gg/JnMJV33QHu)

---

Built with ?? by the Buildersoft team.
