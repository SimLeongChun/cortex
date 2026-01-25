using Cortex.Mediator;
using Cortex.Mediator.Commands;
using Cortex.Mediator.DependencyInjection;
using Cortex.Mediator.Notifications;
using Cortex.Mediator.Processors;
using Cortex.Mediator.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Tests.Mediator.Tests
{
    public class ProcessorIntegrationTests
    {
        #region Test Commands, Queries, Notifications

        public class CreateOrderCommand : ICommand<int>
        {
            public string ProductName { get; set; } = string.Empty;
            public int Quantity { get; set; }
        }

        public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, int>
        {
            public Task<int> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
            {
                return Task.FromResult(123); // Return order ID
            }
        }

        public class DeleteOrderCommand : ICommand
        {
            public int OrderId { get; set; }
        }

        public class DeleteOrderCommandHandler : ICommandHandler<DeleteOrderCommand>
        {
            public Task Handle(DeleteOrderCommand command, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        public class GetOrderQuery : IQuery<OrderDto>
        {
            public int OrderId { get; set; }
        }

        public class OrderDto
        {
            public int Id { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        public class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, OrderDto>
        {
            public Task<OrderDto> Handle(GetOrderQuery query, CancellationToken cancellationToken)
            {
                return Task.FromResult(new OrderDto { Id = query.OrderId, Status = "Pending" });
            }
        }

        public class OrderCreatedNotification : INotification
        {
            public int OrderId { get; set; }
        }

        public class OrderCreatedNotificationHandler : INotificationHandler<OrderCreatedNotification>
        {
            public Task Handle(OrderCreatedNotification notification, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        // Pre/Post Processors
        public class TestExecutionLog
        {
            public List<string> Entries { get; } = new();
        }

        public class CreateOrderPreProcessor : IRequestPreProcessor<CreateOrderCommand>
        {
            private readonly TestExecutionLog _log;

            public CreateOrderPreProcessor(TestExecutionLog log)
            {
                _log = log;
            }

            public Task ProcessAsync(CreateOrderCommand request, CancellationToken cancellationToken)
            {
                _log.Entries.Add($"PreProcessor: Creating order for {request.ProductName}");
                return Task.CompletedTask;
            }
        }

        public class CreateOrderPostProcessor : IRequestPostProcessor<CreateOrderCommand, int>
        {
            private readonly TestExecutionLog _log;

            public CreateOrderPostProcessor(TestExecutionLog log)
            {
                _log = log;
            }

            public Task ProcessAsync(CreateOrderCommand request, int response, CancellationToken cancellationToken)
            {
                _log.Entries.Add($"PostProcessor: Order {response} created for {request.ProductName}");
                return Task.CompletedTask;
            }
        }

        public class DeleteOrderPostProcessor : IRequestPostProcessor<DeleteOrderCommand>
        {
            private readonly TestExecutionLog _log;

            public DeleteOrderPostProcessor(TestExecutionLog log)
            {
                _log = log;
            }

            public Task ProcessAsync(DeleteOrderCommand request, CancellationToken cancellationToken)
            {
                _log.Entries.Add($"PostProcessor: Order {request.OrderId} deleted");
                return Task.CompletedTask;
            }
        }

        public class GetOrderPreProcessor : IRequestPreProcessor<GetOrderQuery>
        {
            private readonly TestExecutionLog _log;

            public GetOrderPreProcessor(TestExecutionLog log)
            {
                _log = log;
            }

            public Task ProcessAsync(GetOrderQuery request, CancellationToken cancellationToken)
            {
                _log.Entries.Add($"PreProcessor: Getting order {request.OrderId}");
                return Task.CompletedTask;
            }
        }

        public class GetOrderPostProcessor : IRequestPostProcessor<GetOrderQuery, OrderDto>
        {
            private readonly TestExecutionLog _log;

            public GetOrderPostProcessor(TestExecutionLog log)
            {
                _log = log;
            }

            public Task ProcessAsync(GetOrderQuery request, OrderDto response, CancellationToken cancellationToken)
            {
                _log.Entries.Add($"PostProcessor: Order {response.Id} retrieved with status {response.Status}");
                return Task.CompletedTask;
            }
        }

        public class OrderCreatedPreProcessor : IRequestPreProcessor<OrderCreatedNotification>
        {
            private readonly TestExecutionLog _log;

            public OrderCreatedPreProcessor(TestExecutionLog log)
            {
                _log = log;
            }

            public Task ProcessAsync(OrderCreatedNotification request, CancellationToken cancellationToken)
            {
                _log.Entries.Add($"PreProcessor: Notification for order {request.OrderId}");
                return Task.CompletedTask;
            }
        }

        #endregion

        private IServiceProvider CreateServiceProvider(TestExecutionLog log)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton(log);

            services.AddCortexMediator(
                Array.Empty<Type>(),
                options => options.AddProcessorBehaviors());

            // Register handlers
            services.AddTransient<ICommandHandler<CreateOrderCommand, int>, CreateOrderCommandHandler>();
            services.AddTransient<ICommandHandler<DeleteOrderCommand>, DeleteOrderCommandHandler>();
            services.AddTransient<IQueryHandler<GetOrderQuery, OrderDto>, GetOrderQueryHandler>();
            services.AddTransient<INotificationHandler<OrderCreatedNotification>, OrderCreatedNotificationHandler>();

            // Register processors
            services.AddTransient<IRequestPreProcessor<CreateOrderCommand>, CreateOrderPreProcessor>();
            services.AddTransient<IRequestPostProcessor<CreateOrderCommand, int>, CreateOrderPostProcessor>();
            services.AddTransient<IRequestPostProcessor<DeleteOrderCommand>, DeleteOrderPostProcessor>();
            services.AddTransient<IRequestPreProcessor<GetOrderQuery>, GetOrderPreProcessor>();
            services.AddTransient<IRequestPostProcessor<GetOrderQuery, OrderDto>, GetOrderPostProcessor>();
            services.AddTransient<IRequestPreProcessor<OrderCreatedNotification>, OrderCreatedPreProcessor>();

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Command_WithPreAndPostProcessors_ShouldExecuteInOrder()
        {
            // Arrange
            var log = new TestExecutionLog();
            var provider = CreateServiceProvider(log);
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var orderId = await mediator.SendAsync(new CreateOrderCommand
            {
                ProductName = "Widget",
                Quantity = 5
            });

            // Assert
            Assert.Equal(123, orderId);
            Assert.Equal(2, log.Entries.Count);
            Assert.Contains("PreProcessor: Creating order for Widget", log.Entries[0]);
            Assert.Contains("PostProcessor: Order 123 created for Widget", log.Entries[1]);
        }

        [Fact]
        public async Task VoidCommand_WithPostProcessor_ShouldExecute()
        {
            // Arrange
            var log = new TestExecutionLog();
            var provider = CreateServiceProvider(log);
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            await mediator.SendAsync(new DeleteOrderCommand { OrderId = 456 });

            // Assert
            Assert.Single(log.Entries);
            Assert.Contains("PostProcessor: Order 456 deleted", log.Entries[0]);
        }

        [Fact]
        public async Task Query_WithPreAndPostProcessors_ShouldExecuteInOrder()
        {
            // Arrange
            var log = new TestExecutionLog();
            var provider = CreateServiceProvider(log);
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var order = await mediator.QueryAsync(new GetOrderQuery { OrderId = 789 });

            // Assert
            Assert.Equal(789, order.Id);
            Assert.Equal("Pending", order.Status);
            Assert.Equal(2, log.Entries.Count);
            Assert.Contains("PreProcessor: Getting order 789", log.Entries[0]);
            Assert.Contains("PostProcessor: Order 789 retrieved with status Pending", log.Entries[1]);
        }

        [Fact]
        public async Task Notification_WithPreProcessor_ShouldExecute()
        {
            // Arrange
            var log = new TestExecutionLog();
            var provider = CreateServiceProvider(log);
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            await mediator.PublishAsync(new OrderCreatedNotification { OrderId = 999 });

            // Assert
            Assert.Single(log.Entries);
            Assert.Contains("PreProcessor: Notification for order 999", log.Entries[0]);
        }

        [Fact]
        public async Task MultipleProcessors_ShouldAllExecute()
        {
            // Arrange
            var log = new TestExecutionLog();
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton(log);

            services.AddCortexMediator(Array.Empty<Type>(), options => options.AddProcessorBehaviors());
            services.AddTransient<ICommandHandler<CreateOrderCommand, int>, CreateOrderCommandHandler>();

            // Register multiple pre-processors
            services.AddTransient<IRequestPreProcessor<CreateOrderCommand>>(sp =>
                new TestPreProcessor(sp.GetRequiredService<TestExecutionLog>(), "First"));
            services.AddTransient<IRequestPreProcessor<CreateOrderCommand>>(sp =>
                new TestPreProcessor(sp.GetRequiredService<TestExecutionLog>(), "Second"));

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            await mediator.SendAsync(new CreateOrderCommand { ProductName = "Test" });

            // Assert
            Assert.Equal(2, log.Entries.Count);
            Assert.Contains("First", log.Entries[0]);
            Assert.Contains("Second", log.Entries[1]);
        }

        private class TestPreProcessor : IRequestPreProcessor<CreateOrderCommand>
        {
            private readonly TestExecutionLog _log;
            private readonly string _name;

            public TestPreProcessor(TestExecutionLog log, string name)
            {
                _log = log;
                _name = name;
            }

            public Task ProcessAsync(CreateOrderCommand request, CancellationToken cancellationToken)
            {
                _log.Entries.Add($"{_name} PreProcessor");
                return Task.CompletedTask;
            }
        }
    }
}
