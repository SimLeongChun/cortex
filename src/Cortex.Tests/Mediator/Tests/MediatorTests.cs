using Cortex.Mediator;
using Cortex.Mediator.Commands;
using Cortex.Mediator.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Tests.Mediator.Tests
{
    #region Test Commands and Queries

    public class CreateUserCommand : ICommand<Guid>
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, Guid>
    {
        public Task<Guid> Handle(CreateUserCommand command, CancellationToken cancellationToken)
        {
            // Simulate creating a user and returning a new Guid
            return Task.FromResult(Guid.NewGuid());
        }
    }

    public class DeleteUserCommand : ICommand
    {
        public Guid UserId { get; set; }
    }

    public class DeleteUserCommandHandler : ICommandHandler<DeleteUserCommand>
    {
        public bool WasHandled { get; private set; }

        public Task Handle(DeleteUserCommand command, CancellationToken cancellationToken)
        {
            WasHandled = true;
            return Task.CompletedTask;
        }
    }

    public class GetUserQuery : IQuery<UserDto>
    {
        public Guid UserId { get; set; }
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class GetUserQueryHandler : IQueryHandler<GetUserQuery, UserDto>
    {
        public Task<UserDto> Handle(GetUserQuery query, CancellationToken cancellationToken)
        {
            return Task.FromResult(new UserDto
            {
                Id = query.UserId,
                Name = "Test User",
                Email = "test@example.com"
            });
        }
    }

    public class GetUsersCountQuery : IQuery<int>
    {
    }

    public class GetUsersCountQueryHandler : IQueryHandler<GetUsersCountQuery, int>
    {
        public Task<int> Handle(GetUsersCountQuery query, CancellationToken cancellationToken)
        {
            return Task.FromResult(42);
        }
    }

    #endregion

    public class MediatorTests
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMediator _mediator;

        public MediatorTests()
        {
            var services = new ServiceCollection();
            
            // Register the mediator
            services.AddSingleton<IMediator, Cortex.Mediator.Mediator>();
            
            // Register command handlers
            services.AddTransient<ICommandHandler<CreateUserCommand, Guid>, CreateUserCommandHandler>();
            services.AddSingleton<DeleteUserCommandHandler>();
            services.AddTransient<ICommandHandler<DeleteUserCommand>>(sp => sp.GetRequiredService<DeleteUserCommandHandler>());
            
            // Register query handlers
            services.AddTransient<IQueryHandler<GetUserQuery, UserDto>, GetUserQueryHandler>();
            services.AddTransient<IQueryHandler<GetUsersCountQuery, int>, GetUsersCountQueryHandler>();

            _serviceProvider = services.BuildServiceProvider();
            _mediator = _serviceProvider.GetRequiredService<IMediator>();
        }

        #region SendAsync Extension Method Tests (Commands with Result)

        [Fact]
        public async Task SendAsync_WithCommandThatReturnsValue_ShouldReturnResult()
        {
            // Arrange
            var command = new CreateUserCommand { Name = "John Doe", Email = "john@example.com" };

            // Act - Using the new simplified API via extension method
            var result = await _mediator.SendAsync(command);

            // Assert
            Assert.NotEqual(Guid.Empty, result);
        }

        [Fact]
        public async Task SendAsync_WithCommandThatReturnsValue_TypeIsInferredCorrectly()
        {
            // Arrange
            var command = new CreateUserCommand { Name = "Jane Doe", Email = "jane@example.com" };

            // Act - The result type (Guid) is inferred from the command
            Guid result = await _mediator.SendAsync(command);

            // Assert - This test verifies type inference works correctly at compile time
            Assert.IsType<Guid>(result);
        }

        [Fact]
        public async Task SendAsync_WithNullCommand_ShouldThrowArgumentNullException()
        {
            // Arrange
            ICommand<Guid> command = null!;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _mediator.SendAsync(command));
        }

        #endregion

        #region SendAsync Extension Method Tests (Void Commands)

        [Fact]
        public async Task SendAsync_WithVoidCommand_ShouldExecuteHandler()
        {
            // Arrange
            var command = new DeleteUserCommand { UserId = Guid.NewGuid() };
            var handler = _serviceProvider.GetRequiredService<DeleteUserCommandHandler>();

            // Act - Using the new simplified API
            await _mediator.SendAsync(command);

            // Assert
            Assert.True(handler.WasHandled);
        }

        [Fact]
        public async Task SendAsync_WithVoidNullCommand_ShouldThrowArgumentNullException()
        {
            // Arrange
            ICommand command = null!;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _mediator.SendAsync(command));
        }

        #endregion

        #region QueryAsync Extension Method Tests

        [Fact]
        public async Task QueryAsync_WithQuery_ShouldReturnResult()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = new GetUserQuery { UserId = userId };

            // Act - Using the new simplified API
            var result = await _mediator.QueryAsync(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(userId, result.Id);
            Assert.Equal("Test User", result.Name);
            Assert.Equal("test@example.com", result.Email);
        }

        [Fact]
        public async Task QueryAsync_WithQuery_TypeIsInferredCorrectly()
        {
            // Arrange
            var query = new GetUsersCountQuery();

            // Act - The result type (int) is inferred from the query
            int result = await _mediator.QueryAsync(query);

            // Assert - This test verifies type inference works correctly
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task QueryAsync_WithNullQuery_ShouldThrowArgumentNullException()
        {
            // Arrange
            IQuery<UserDto> query = null!;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _mediator.QueryAsync(query));
        }

        #endregion

        #region Direct Interface Method Tests (Non-extension)

        [Fact]
        public async Task SendCommandAsync_WithInferredType_ShouldReturnResult()
        {
            // Arrange
            ICommand<Guid> command = new CreateUserCommand { Name = "Test", Email = "test@test.com" };

            // Act - Using the new interface method directly
            var result = await _mediator.SendCommandAsync(command);

            // Assert
            Assert.NotEqual(Guid.Empty, result);
        }

        [Fact]
        public async Task SendCommandAsync_WithVoidInferredType_ShouldExecute()
        {
            // Arrange
            ICommand command = new DeleteUserCommand { UserId = Guid.NewGuid() };
            var handler = _serviceProvider.GetRequiredService<DeleteUserCommandHandler>();

            // Act - Using the new interface method directly
            await _mediator.SendCommandAsync(command);

            // Assert
            Assert.True(handler.WasHandled);
        }

        [Fact]
        public async Task SendQueryAsync_WithInferredType_ShouldReturnResult()
        {
            // Arrange
            IQuery<int> query = new GetUsersCountQuery();

            // Act - Using the new interface method directly
            var result = await _mediator.SendQueryAsync(query);

            // Assert
            Assert.Equal(42, result);
        }

        #endregion

        #region Backward Compatibility Tests

        [Fact]
        public async Task SendCommandAsync_WithExplicitTypeParameters_ShouldStillWork()
        {
            // Arrange
            var command = new CreateUserCommand { Name = "Legacy", Email = "legacy@test.com" };

            // Act - Using the original API with explicit type parameters
            var result = await _mediator.SendCommandAsync<CreateUserCommand, Guid>(command);

            // Assert
            Assert.NotEqual(Guid.Empty, result);
        }

        [Fact]
        public async Task SendQueryAsync_WithExplicitTypeParameters_ShouldStillWork()
        {
            // Arrange
            var query = new GetUserQuery { UserId = Guid.NewGuid() };

            // Act - Using the original API with explicit type parameters
            var result = await _mediator.SendQueryAsync<GetUserQuery, UserDto>(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test User", result.Name);
        }

        #endregion

        #region Cancellation Token Tests

        [Fact]
        public async Task SendAsync_WithCancellationToken_ShouldPassTokenToHandler()
        {
            // Arrange
            var command = new CreateUserCommand { Name = "Test", Email = "test@test.com" };
            using var cts = new CancellationTokenSource();

            // Act - The token should be passed through
            var result = await _mediator.SendAsync(command, cts.Token);

            // Assert
            Assert.NotEqual(Guid.Empty, result);
        }

        [Fact]
        public async Task QueryAsync_WithCancellationToken_ShouldPassTokenToHandler()
        {
            // Arrange
            var query = new GetUsersCountQuery();
            using var cts = new CancellationTokenSource();

            // Act - The token should be passed through
            var result = await _mediator.QueryAsync(query, cts.Token);

            // Assert
            Assert.Equal(42, result);
        }

        #endregion
    }
}
