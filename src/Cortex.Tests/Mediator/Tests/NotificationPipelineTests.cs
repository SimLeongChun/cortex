using Cortex.Mediator;
using Cortex.Mediator.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Tests.Mediator.Tests
{
    #region Test Notifications, Handlers, and Behaviors

    public class TestNotification : INotification
    {
        public string Message { get; set; } = string.Empty;
    }

    public class TestNotificationHandler1 : INotificationHandler<TestNotification>
    {
        private readonly List<string> _log;

        public TestNotificationHandler1(List<string> log)
        {
            _log = log;
        }

        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            _log.Add($"Handler1: {notification.Message}");
            return Task.CompletedTask;
        }
    }

    public class TestNotificationHandler2 : INotificationHandler<TestNotification>
    {
        private readonly List<string> _log;

        public TestNotificationHandler2(List<string> log)
        {
            _log = log;
        }

        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            _log.Add($"Handler2: {notification.Message}");
            return Task.CompletedTask;
        }
    }

    public class TestNotificationPipelineBehavior<TNotification> : INotificationPipelineBehavior<TNotification>
        where TNotification : INotification
    {
        private readonly List<string> _log;

        public TestNotificationPipelineBehavior(List<string> log)
        {
            _log = log;
        }

        public async Task Handle(TNotification notification, NotificationHandlerDelegate next, CancellationToken cancellationToken)
        {
            _log.Add("Before Notification");
            await next();
            _log.Add("After Notification");
        }
    }

    public class NamedNotificationPipelineBehavior<TNotification> : INotificationPipelineBehavior<TNotification>
        where TNotification : INotification
    {
        private readonly string _name;
        private readonly List<string> _log;

        public NamedNotificationPipelineBehavior(string name, List<string> log)
        {
            _name = name;
            _log = log;
        }

        public async Task Handle(TNotification notification, NotificationHandlerDelegate next, CancellationToken cancellationToken)
        {
            _log.Add($"Before {_name}");
            await next();
            _log.Add($"After {_name}");
        }
    }

    public class ErrorHandlingNotificationBehavior<TNotification> : INotificationPipelineBehavior<TNotification>
        where TNotification : INotification
    {
        private readonly List<string> _log;

        public ErrorHandlingNotificationBehavior(List<string> log)
        {
            _log = log;
        }

        public async Task Handle(TNotification notification, NotificationHandlerDelegate next, CancellationToken cancellationToken)
        {
            try
            {
                _log.Add("Error Handler: Before");
                await next();
                _log.Add("Error Handler: After");
            }
            catch (Exception ex)
            {
                _log.Add($"Error Handler: Caught {ex.Message}");
                throw;
            }
        }
    }

    public class ThrowingNotificationHandler : INotificationHandler<TestNotification>
    {
        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Handler threw an exception");
        }
    }

    #endregion

    public class NotificationPipelineTests
    {
        [Fact]
        public async Task PublishAsync_WithPipelineBehavior_ShouldExecuteBehaviorAndHandler()
        {
            // Arrange
            var log = new List<string>();
            var services = new ServiceCollection();

            services.AddSingleton<IMediator, Cortex.Mediator.Mediator>();
            services.AddTransient<INotificationHandler<TestNotification>>(sp =>
                new TestNotificationHandler1(log));
            services.AddTransient<INotificationPipelineBehavior<TestNotification>>(sp =>
                new TestNotificationPipelineBehavior<TestNotification>(log));

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var notification = new TestNotification { Message = "Test" };

            // Act
            await mediator.PublishAsync(notification);

            // Assert
            Assert.Equal(3, log.Count);
            Assert.Equal("Before Notification", log[0]);
            Assert.Equal("Handler1: Test", log[1]);
            Assert.Equal("After Notification", log[2]);
        }

        [Fact]
        public async Task PublishAsync_WithMultipleHandlers_ShouldApplyBehaviorToEachHandler()
        {
            // Arrange
            var log = new List<string>();
            var services = new ServiceCollection();

            services.AddSingleton<IMediator, Cortex.Mediator.Mediator>();
            services.AddTransient<INotificationHandler<TestNotification>>(sp =>
                new TestNotificationHandler1(log));
            services.AddTransient<INotificationHandler<TestNotification>>(sp =>
                new TestNotificationHandler2(log));
            services.AddTransient<INotificationPipelineBehavior<TestNotification>>(sp =>
                new TestNotificationPipelineBehavior<TestNotification>(log));

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var notification = new TestNotification { Message = "Test" };

            // Act
            await mediator.PublishAsync(notification);

            // Assert - Each handler should have its own pipeline behavior execution
            Assert.Contains("Before Notification", log);
            Assert.Contains("After Notification", log);
            Assert.Contains("Handler1: Test", log);
            Assert.Contains("Handler2: Test", log);
            // With 2 handlers, we expect 2 "Before" and 2 "After" entries
            Assert.Equal(2, log.Count(l => l == "Before Notification"));
            Assert.Equal(2, log.Count(l => l == "After Notification"));
        }

        [Fact]
        public async Task PublishAsync_WithMultipleBehaviors_ShouldExecuteInCorrectOrder()
        {
            // Arrange
            var log = new List<string>();
            var services = new ServiceCollection();

            services.AddSingleton<IMediator, Cortex.Mediator.Mediator>();
            services.AddTransient<INotificationHandler<TestNotification>>(sp =>
                new TestNotificationHandler1(log));
            services.AddTransient<INotificationPipelineBehavior<TestNotification>>(sp =>
                new NamedNotificationPipelineBehavior<TestNotification>("First", log));
            services.AddTransient<INotificationPipelineBehavior<TestNotification>>(sp =>
                new NamedNotificationPipelineBehavior<TestNotification>("Second", log));

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var notification = new TestNotification { Message = "Test" };

            // Act
            await mediator.PublishAsync(notification);

            // Assert - Behaviors are applied in reverse registration order
            Assert.Equal(5, log.Count);
            Assert.Equal("Before First", log[0]);
            Assert.Equal("Before Second", log[1]);
            Assert.Equal("Handler1: Test", log[2]);
            Assert.Equal("After Second", log[3]);
            Assert.Equal("After First", log[4]);
        }

        [Fact]
        public async Task PublishAsync_WithErrorHandlingBehavior_ShouldCatchAndRethrow()
        {
            // Arrange
            var log = new List<string>();
            var services = new ServiceCollection();

            services.AddSingleton<IMediator, Cortex.Mediator.Mediator>();
            services.AddTransient<INotificationHandler<TestNotification>, ThrowingNotificationHandler>();
            services.AddTransient<INotificationPipelineBehavior<TestNotification>>(sp =>
                new ErrorHandlingNotificationBehavior<TestNotification>(log));

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var notification = new TestNotification { Message = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.PublishAsync(notification));
            Assert.Contains("Error Handler: Before", log);
            Assert.Contains("Error Handler: Caught Handler threw an exception", log);
        }

        [Fact]
        public async Task PublishAsync_WithNoBehaviors_ShouldExecuteHandlerDirectly()
        {
            // Arrange
            var log = new List<string>();
            var services = new ServiceCollection();

            services.AddSingleton<IMediator, Cortex.Mediator.Mediator>();
            services.AddTransient<INotificationHandler<TestNotification>>(sp =>
                new TestNotificationHandler1(log));

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var notification = new TestNotification { Message = "Direct" };

            // Act
            await mediator.PublishAsync(notification);

            // Assert
            Assert.Single(log);
            Assert.Equal("Handler1: Direct", log[0]);
        }

        [Fact]
        public async Task PublishAsync_WithNoHandlers_ShouldCompleteSuccessfully()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IMediator, Cortex.Mediator.Mediator>();

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var notification = new TestNotification { Message = "NoHandler" };

            // Act & Assert - Should not throw
            await mediator.PublishAsync(notification);
        }
    }
}
