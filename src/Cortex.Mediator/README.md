# Cortex.Mediator 🧠

**Cortex.Mediator** is a lightweight and extensible implementation of the Mediator pattern for .NET applications, designed to power clean, modular architectures like **Vertical Slice Architecture** and **CQRS**.


Built as part of the [Cortex Data Framework](https://github.com/buildersoftio/cortex), this library simplifies command and query handling with built-in support for:


- ✅ Commands & Queries
- ✅ Notifications (Events)
- ✅ Pipeline Behaviors
- ✅ FluentValidation
- ✅ Logging

---

[![GitHub License](https://img.shields.io/github/license/buildersoftio/cortex)](https://github.com/buildersoftio/cortex/blob/master/LICENSE)
[![NuGet Version](https://img.shields.io/nuget/v/Cortex.Mediator?label=Cortex.Mediator)](https://www.nuget.org/packages/Cortex.Mediator)
[![GitHub contributors](https://img.shields.io/github/contributors/buildersoftio/cortex)](https://github.com/buildersoftio/cortex)
[![Discord Shield](https://discord.com/api/guilds/1310034212371566612/widget.png?style=shield)](https://discord.gg/JnMJV33QHu)


## 🚀 Getting Started

### Install via NuGet

```bash
dotnet add package Cortex.Mediator
```

## 🛠️ Setup
In `Program.cs` or `Startup.cs`:
```csharp
builder.Services.AddCortexMediator(
    new[] { typeof(Program) }, // Assemblies to scan for handlers
    options => options.AddDefaultBehaviors() // Logging
);
```

## 📦 Folder Structure Example (Vertical Slice)
```bash
Features/
  CreateUser/
    CreateUserCommand.cs
    CreateUserCommandHandler.cs
    CreateUserValidator.cs
    CreateUserEndpoint.cs
```

## ✏️ Defining a Command

```csharp
public class CreateUserCommand : ICommand<Guid>
{
    public string UserName { get; set; }
    public string Email { get; set; }
}
```

### Handler
```csharp
public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand,Guid>
{
    public async Task<Guid> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        // Logic here
    }
}
```

### Sending Commands

**Simplified API (Recommended)** - Type is automatically inferred:
```csharp
// Using extension methods - no need to specify type parameters!
var userId = await mediator.SendAsync(command);

// For void commands (no return value)
await mediator.SendAsync(new DeleteUserCommand { UserId = userId });
```

**Explicit Type Parameters** (Legacy):
```csharp
var userId = await mediator.SendCommandAsync<CreateUserCommand, Guid>(command);
```

### Validator (Optional, via FluentValidation)
```csharp
public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.UserName).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
```

---

## 🔍 Defining a Query

```csharp
public class GetUserQuery : IQuery<GetUserResponse>
{
    public int UserId { get; set; }
}
```
```csharp
public class GetUserQueryHandler : IQueryHandler<GetUserQuery, GetUserResponse>
{
    public async Task<GetUserResponse> Handle(GetUserQuery query, CancellationToken cancellationToken)
    {
        return new GetUserResponse { UserId = query.UserId, UserName = "Andy" };
    }
}

```

### Sending Queries

**Simplified API (Recommended)** - Type is automatically inferred:
```csharp
// Using extension methods - no need to specify type parameters!
var user = await mediator.QueryAsync(new GetUserQuery { UserId = 1 });
```

**Explicit Type Parameters** (Legacy):
```csharp
var user = await mediator.SendQueryAsync<GetUserQuery, GetUserResponse>(query);
```

## 📢 Notifications (Events)

```csharp
public class UserCreatedNotification : INotification
{
    public string UserName { get; set; }
}

public class SendWelcomeEmailHandler : INotificationHandler<UserCreatedNotification>
{
    public async Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        // Send email...
    }
}
```
```csharp
await mediator.PublishAsync(new UserCreatedNotification { UserName = "Andy" });
```

## 🔧 Pipeline Behaviors (Built-in)
Out of the box, Cortex.Mediator supports:

- `LoggingCommandBehavior` - Logs command execution with timing
- `LoggingQueryBehavior` - Logs query execution with timing
- `LoggingNotificationBehavior` - Logs notification publishing with timing
- `ExceptionHandlingCommandBehavior` - Centralized exception handling for commands
- `ExceptionHandlingQueryBehavior` - Centralized exception handling for queries
- `ExceptionHandlingNotificationBehavior` - Centralized exception handling for notifications
- `ValidationCommandBehavior` - FluentValidation support (via `Cortex.Mediator.Behaviors.FluentValidation`)

### Registering Behaviors
```csharp
// Add default logging behaviors
options.AddDefaultBehaviors();

// Add exception handling behaviors
options.AddExceptionHandlingBehaviors();

// Add both logging and exception handling
options.AddDefaultBehaviorsWithExceptionHandling();

// Custom behaviors
options.AddOpenCommandPipelineBehavior(typeof(MyCustomBehavior<,>));
options.AddOpenQueryPipelineBehavior(typeof(MyCustomQueryBehavior<,>));
options.AddOpenNotificationPipelineBehavior(typeof(MyCustomNotificationBehavior<>));
```

## ⚠️ Exception Handling Behavior
The exception handling behaviors provide centralized exception handling with optional fallback results.

### Basic Setup
```csharp
builder.Services.AddCortexMediator(
    new[] { typeof(Program) },
    options => options.AddExceptionHandlingBehaviors()
);
```

### Custom Exception Handler
Implement `IExceptionHandler` to customize exception handling:
```csharp
public class MyExceptionHandler : IExceptionHandler
{
    private readonly ILogger<MyExceptionHandler> _logger;

    public MyExceptionHandler(ILogger<MyExceptionHandler> logger)
    {
        _logger = logger;
    }

    public Task<bool> HandleAsync(
        Exception exception,
        Type requestType,
        object request,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Error processing {RequestType}", requestType.Name);
        
        // Return true to suppress the exception, false to rethrow
        return Task.FromResult(false);
    }
}

// Register in DI
services.AddSingleton<IExceptionHandler, MyExceptionHandler>();
```

### Exception Handler with Fallback Result
For commands and queries that return a value, implement `IExceptionHandler<TResult>`:
```csharp
public class FallbackExceptionHandler : IExceptionHandler<ApiResponse>
{
    public Task<(bool handled, ApiResponse? result)> HandleWithResultAsync(
        Exception exception,
        Type requestType,
        object request,
        CancellationToken cancellationToken)
    {
        var fallback = new ApiResponse 
        { 
            Success = false, 
            Error = exception.Message 
        };
        
        return Task.FromResult((true, fallback));
    }

    public Task<bool> HandleAsync(Exception exception, Type requestType, object request, CancellationToken cancellationToken)
        => Task.FromResult(false);
}
```

### Notification Exception Suppression
For notifications, you can suppress exceptions to allow other handlers to continue:
```csharp
// The ExceptionHandlingNotificationBehavior has a suppressExceptions parameter
// When true, exceptions are logged but not rethrown
```

## 💾 Caching Behavior for Queries
The caching behavior provides automatic caching of query results to improve performance.

### Basic Setup
```csharp
// Add caching services
builder.Services.AddMediatorCaching(options =>
{
    options.DefaultAbsoluteExpiration = TimeSpan.FromMinutes(5);
    options.DefaultSlidingExpiration = TimeSpan.FromMinutes(1);
    options.CacheKeyPrefix = "MyApp";
});

// Add mediator with caching behavior
builder.Services.AddCortexMediator(
    new[] { typeof(Program) },
    options => options.AddCachingBehavior()
);
```

### Using the Cacheable Attribute
Mark your query classes with the `[Cacheable]` attribute:
```csharp
[Cacheable(AbsoluteExpirationSeconds = 300, SlidingExpirationSeconds = 60)]
public class GetUserQuery : IQuery<UserDto>
{
    public int UserId { get; set; }
}
```

### Using the ICacheableQuery Interface
For more control, implement `ICacheableQuery`:
```csharp
public class GetProductQuery : IQuery<ProductDto>, ICacheableQuery
{
    public int ProductId { get; set; }
    
    // Custom cache key
    public string? CacheKey => $"product-{ProductId}";
    
    // Custom expiration times
    public TimeSpan? AbsoluteExpiration => TimeSpan.FromMinutes(10);
    public TimeSpan? SlidingExpiration => TimeSpan.FromMinutes(2);
}
```

### Cache Invalidation
Use `ICacheInvalidator` to manually invalidate cached results:
```csharp
public class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand>
{
    private readonly ICacheInvalidator _cacheInvalidator;

    public UpdateUserCommandHandler(ICacheInvalidator cacheInvalidator)
    {
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        // Update user in database...
        
        // Invalidate the cached query result
        _cacheInvalidator.Invalidate<GetUserQuery, UserDto>(
            new GetUserQuery { UserId = command.UserId });
    }
}
```

### Custom Cache Key Generator
Implement `ICacheKeyGenerator` for custom key generation:
```csharp
public class MyCacheKeyGenerator : ICacheKeyGenerator
{
    public string GenerateKey<TQuery, TResult>(TQuery query) 
        where TQuery : IQuery<TResult>
    {
        // Custom key generation logic
        return $"MyApp:{typeof(TQuery).Name}:{query.GetHashCode()}";
    }
}

// Register custom generator
services.AddMediatorCaching<MyCacheKeyGenerator>();
```

## 💬 Contributing
We welcome contributions from the community! Whether it's reporting bugs, suggesting features, or submitting pull requests, your involvement helps improve Cortex for everyone.

### 💬 How to Contribute
1. **Fork the Repository**
2. **Create a Feature Branch**
```bash
git checkout -b feature/YourFeature
```
3. **Commit Your Changes**
```bash
git commit -m "Add your feature"
```
4. **Push to Your Fork**
```bash
git push origin feature/YourFeature
```
5. **Open a Pull Request**

Describe your changes and submit the pull request for review.

## 📄 License
This project is licensed under the MIT License.

## 📚 Sponsorship
Cortex is an open-source project maintained by BuilderSoft. Your support helps us continue developing and improving Cortex. Consider sponsoring us to contribute to the future of resilient streaming platforms.

### How to Sponsor
* **Financial Contributions**: Support us through [GitHub Sponsors](https://github.com/sponsors/buildersoftio) or other preferred platforms.
* **Corporate Sponsorship**: If your organization is interested in sponsoring Cortex, please contact us directly.

Contact Us: cortex@buildersoft.io


## Contact
We'd love to hear from you! Whether you have questions, feedback, or need support, feel free to reach out.

- Email: cortex@buildersoft.io
- Website: https://buildersoft.io
- GitHub Issues: [Cortex Data Framework Issues](https://github.com/buildersoftio/cortex/issues)
- Join our Discord Community: [![Discord Shield](https://discord.com/api/guilds/1310034212371566612/widget.png?style=shield)](https://discord.gg/JnMJV33QHu)


Thank you for using Cortex Data Framework! We hope it empowers you to build scalable and efficient data processing pipelines effortlessly.

Built with ❤️ by the Buildersoft team.
