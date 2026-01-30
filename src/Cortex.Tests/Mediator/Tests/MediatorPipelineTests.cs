using Cortex.Mediator;
using Cortex.Mediator.Commands;
using Cortex.Mediator.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Tests.Mediator.Tests
{
    #region Test Commands, Queries, and Behaviors for Pipeline Tests

    public class LoggedCommand : ICommand<string>
    {
        public string Input { get; set; } = string.Empty;
    }

    public class LoggedCommandHandler : ICommandHandler<LoggedCommand, string>
    {
        public Task<string> Handle(LoggedCommand command, CancellationToken cancellationToken)
        {
            return Task.FromResult($"Processed: {command.Input}");
        }
    }

    public class LoggedVoidCommand : ICommand
    {
        public string Input { get; set; } = string.Empty;
    }

    public class LoggedVoidCommandHandler : ICommandHandler<LoggedVoidCommand>
    {
        public List<string> ExecutionLog { get; } = new();

        public Task Handle(LoggedVoidCommand command, CancellationToken cancellationToken)
        {
            ExecutionLog.Add($"Handler: {command.Input}");
            return Task.CompletedTask;
        }
    }

    public class LoggedQuery : IQuery<string>
    {
        public string Input { get; set; } = string.Empty;
    }

    public class LoggedQueryHandler : IQueryHandler<LoggedQuery, string>
    {
        public Task<string> Handle(LoggedQuery query, CancellationToken cancellationToken)
        {
            return Task.FromResult($"Query Result: {query.Input}");
        }
    }

    /// <summary>
    /// Test pipeline behavior that logs before and after execution
    /// </summary>
    public class TestCommandPipelineBehavior<TCommand, TResult> : ICommandPipelineBehavior<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        private readonly List<string> _log;

        public TestCommandPipelineBehavior(List<string> log)
        {
            _log = log;
        }

        public async Task<TResult> Handle(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            _log.Add("Before Command");
            var result = await next();
            _log.Add("After Command");
            return result;
        }
    }

    public class TestVoidCommandPipelineBehavior<TCommand> : ICommandPipelineBehavior<TCommand>
        where TCommand : ICommand
    {
        private readonly List<string> _log;

        public TestVoidCommandPipelineBehavior(List<string> log)
        {
            _log = log;
        }

        public async Task Handle(TCommand command, CommandHandlerDelegate next, CancellationToken cancellationToken)
        {
            _log.Add("Before Void Command");
            await next();
            _log.Add("After Void Command");
        }
    }

    public class TestQueryPipelineBehavior<TQuery, TResult> : IQueryPipelineBehavior<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        private readonly List<string> _log;

        public TestQueryPipelineBehavior(List<string> log)
        {
            _log = log;
        }

        public async Task<TResult> Handle(TQuery query, QueryHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            _log.Add("Before Query");
            var result = await next();
            _log.Add("After Query");
            return result;
        }
    }

    #endregion

    public class MediatorPipelineTests
    {
        [Fact]
        public async Task SendAsync_WithPipelineBehavior_ShouldExecuteBehaviorAndHandler()
        {
            // Arrange
            var log = new List<string>();
            var services = new ServiceCollection();
            
            services.AddSingleton<IMediator, Cortex.Mediator.Mediator>();
            services.AddTransient<ICommandHandler<LoggedCommand, string>, LoggedCommandHandler>();
            services.AddTransient<ICommandPipelineBehavior<LoggedCommand, string>>(sp => 
                new TestCommandPipelineBehavior<LoggedCommand, string>(log));

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var command = new LoggedCommand { Input = "Test" };

            // Act
            var result = await mediator.SendAsync(command);

            // Assert
            Assert.Equal("Processed: Test", result);
            Assert.Equal(2, log.Count);
            Assert.Equal("Before Command", log[0]);
            Assert.Equal("After Command", log[1]);
        }

        [Fact]
        public async Task SendAsync_WithVoidCommandAndPipelineBehavior_ShouldExecuteBehaviorAndHandler()
        {
            // Arrange
            var log = new List<string>();
            var services = new ServiceCollection();
            
            services.AddSingleton<IMediator, Cortex.Mediator.Mediator>();
            services.AddSingleton<LoggedVoidCommandHandler>();
            services.AddTransient<ICommandHandler<LoggedVoidCommand>>(sp => 
                sp.GetRequiredService<LoggedVoidCommandHandler>());
            services.AddTransient<ICommandPipelineBehavior<LoggedVoidCommand>>(sp => 
                new TestVoidCommandPipelineBehavior<LoggedVoidCommand>(log));

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();
            var handler = provider.GetRequiredService<LoggedVoidCommandHandler>();

            var command = new LoggedVoidCommand { Input = "VoidTest" };

            // Act
            await mediator.SendAsync(command);

            // Assert
            Assert.Equal(2, log.Count);
            Assert.Equal("Before Void Command", log[0]);
            Assert.Equal("After Void Command", log[1]);
            Assert.Single(handler.ExecutionLog);
            Assert.Equal("Handler: VoidTest", handler.ExecutionLog[0]);
        }

        [Fact]
        public async Task QueryAsync_WithPipelineBehavior_ShouldExecuteBehaviorAndHandler()
        {
            // Arrange
            var log = new List<string>();
            var services = new ServiceCollection();
            
            services.AddSingleton<IMediator, Cortex.Mediator.Mediator>();
            services.AddTransient<IQueryHandler<LoggedQuery, string>, LoggedQueryHandler>();
            services.AddTransient<IQueryPipelineBehavior<LoggedQuery, string>>(sp => 
                new TestQueryPipelineBehavior<LoggedQuery, string>(log));

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var query = new LoggedQuery { Input = "QueryTest" };

            // Act
            var result = await mediator.QueryAsync(query);

            // Assert
            Assert.Equal("Query Result: QueryTest", result);
            Assert.Equal(2, log.Count);
            Assert.Equal("Before Query", log[0]);
            Assert.Equal("After Query", log[1]);
        }

        [Fact]
        public async Task SendAsync_WithMultiplePipelineBehaviors_ShouldExecuteInCorrectOrder()
        {
            // Arrange
            var log = new List<string>();
            var services = new ServiceCollection();
            
            services.AddSingleton<IMediator, Cortex.Mediator.Mediator>();
            services.AddTransient<ICommandHandler<LoggedCommand, string>, LoggedCommandHandler>();
            
            // Register two behaviors - they execute in reverse registration order (like middleware)
            services.AddTransient<ICommandPipelineBehavior<LoggedCommand, string>>(sp => 
                new NamedCommandPipelineBehavior<LoggedCommand, string>("First", log));
            services.AddTransient<ICommandPipelineBehavior<LoggedCommand, string>>(sp => 
                new NamedCommandPipelineBehavior<LoggedCommand, string>("Second", log));

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var command = new LoggedCommand { Input = "Test" };

            // Act
            var result = await mediator.SendAsync(command);

            // Assert
            Assert.Equal("Processed: Test", result);
            Assert.Equal(4, log.Count);
            // Behaviors are applied in reverse registration order, then executed outside-in
            // Registration: First, Second -> Applied in reverse: Second wraps First
            // Execution: First is innermost, Second is outermost
            Assert.Equal("Before First", log[0]);
            Assert.Equal("Before Second", log[1]);
            Assert.Equal("After Second", log[2]);
            Assert.Equal("After First", log[3]);
        }
    }

    /// <summary>
    /// A named pipeline behavior for testing execution order
    /// </summary>
    public class NamedCommandPipelineBehavior<TCommand, TResult> : ICommandPipelineBehavior<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        private readonly string _name;
        private readonly List<string> _log;

        public NamedCommandPipelineBehavior(string name, List<string> log)
        {
            _name = name;
            _log = log;
        }

        public async Task<TResult> Handle(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            _log.Add($"Before {_name}");
            var result = await next();
            _log.Add($"After {_name}");
            return result;
        }
    }
}
