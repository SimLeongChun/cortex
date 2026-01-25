using Cortex.Mediator;
using Cortex.Mediator.Behaviors;
using Cortex.Mediator.DependencyInjection;
using Cortex.Mediator.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Tests.Mediator.Tests
{
    #region Test Types for Integration Tests

    public class IntegrationTestNotification : INotification
    {
        public string Data { get; set; } = string.Empty;
    }

    public class IntegrationNotificationHandler : INotificationHandler<IntegrationTestNotification>
    {
        public static List<string> ExecutionLog { get; } = new();

        public Task Handle(IntegrationTestNotification notification, CancellationToken cancellationToken)
        {
            ExecutionLog.Add($"Handled: {notification.Data}");
            return Task.CompletedTask;
        }
    }

    public class OpenGenericNotificationBehavior<TNotification> : INotificationPipelineBehavior<TNotification>
        where TNotification : INotification
    {
        public static List<string> ExecutionLog { get; } = new();

        public async Task Handle(TNotification notification, NotificationHandlerDelegate next, CancellationToken cancellationToken)
        {
            ExecutionLog.Add($"Before: {typeof(TNotification).Name}");
            await next();
            ExecutionLog.Add($"After: {typeof(TNotification).Name}");
        }
    }

    public class ClosedNotificationBehavior : INotificationPipelineBehavior<IntegrationTestNotification>
    {
        public static List<string> ExecutionLog { get; } = new();

        public async Task Handle(IntegrationTestNotification notification, NotificationHandlerDelegate next, CancellationToken cancellationToken)
        {
            ExecutionLog.Add($"ClosedBehavior Before: {notification.Data}");
            await next();
            ExecutionLog.Add($"ClosedBehavior After: {notification.Data}");
        }
    }

    #endregion

    public class NotificationBehaviorIntegrationTests
    {
        [Fact]
        public void AddOpenNotificationPipelineBehavior_ShouldRegisterBehavior()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddCortexMediator(
                new[] { typeof(IntegrationNotificationHandler) },
                options =>
                {
                    options.AddOpenNotificationPipelineBehavior(typeof(OpenGenericNotificationBehavior<>));
                });

            var provider = services.BuildServiceProvider();

            // Assert
            var behaviors = provider.GetServices<INotificationPipelineBehavior<IntegrationTestNotification>>();
            Assert.Single(behaviors);
        }

        [Fact]
        public void AddNotificationPipelineBehavior_Closed_ShouldRegisterBehavior()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddCortexMediator(
                new[] { typeof(IntegrationNotificationHandler) },
                options =>
                {
                    options.AddNotificationPipelineBehavior<ClosedNotificationBehavior>();
                });

            var provider = services.BuildServiceProvider();

            // Assert
            var behaviors = provider.GetServices<INotificationPipelineBehavior<IntegrationTestNotification>>();
            Assert.Single(behaviors);
        }

        [Fact]
        public void AddNotificationPipelineBehavior_ClosedGeneric_ShouldRegisterSuccessfully()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act - Should NOT throw because OpenGenericNotificationBehavior<IntegrationTestNotification> 
            // is a closed generic type, not an open generic definition
            services.AddCortexMediator(
                new[] { typeof(IntegrationNotificationHandler) },
                options =>
                {
                    options.AddNotificationPipelineBehavior<OpenGenericNotificationBehavior<IntegrationTestNotification>>();
                });

            var provider = services.BuildServiceProvider();

            // Assert - The behavior should be registered
            var behaviors = provider.GetServices<INotificationPipelineBehavior<IntegrationTestNotification>>();
            Assert.Single(behaviors);
        }

        [Fact]
        public void AddOpenNotificationPipelineBehavior_NonOpenGeneric_ShouldThrowArgumentException()
        {
            // Arrange
            var options = new MediatorOptions();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                options.AddOpenNotificationPipelineBehavior(typeof(ClosedNotificationBehavior)));
        }

        [Fact]
        public void AddOpenNotificationPipelineBehavior_NonBehaviorType_ShouldThrowArgumentException()
        {
            // Arrange
            var options = new MediatorOptions();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                options.AddOpenNotificationPipelineBehavior(typeof(List<>)));
        }

        [Fact]
        public async Task IntegrationTest_WithLoggingBehavior_ShouldLogNotificationPublishing()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

            services.AddCortexMediator(
                new[] { typeof(IntegrationNotificationHandler) },
                options =>
                {
                    options.AddOpenNotificationPipelineBehavior(typeof(LoggingNotificationBehavior<>));
                });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var notification = new IntegrationTestNotification { Data = "LoggingTest" };

            // Act - should not throw
            IntegrationNotificationHandler.ExecutionLog.Clear();
            await mediator.PublishAsync(notification);

            // Assert
            Assert.Contains("Handled: LoggingTest", IntegrationNotificationHandler.ExecutionLog);
        }

        [Fact]
        public async Task IntegrationTest_WithOpenGenericBehavior_ShouldExecuteBehavior()
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddCortexMediator(
                new[] { typeof(IntegrationNotificationHandler) },
                options =>
                {
                    options.AddOpenNotificationPipelineBehavior(typeof(OpenGenericNotificationBehavior<>));
                });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var notification = new IntegrationTestNotification { Data = "OpenGenericTest" };

            // Act
            OpenGenericNotificationBehavior<IntegrationTestNotification>.ExecutionLog.Clear();
            IntegrationNotificationHandler.ExecutionLog.Clear();
            await mediator.PublishAsync(notification);

            // Assert
            Assert.Contains("Before: IntegrationTestNotification", OpenGenericNotificationBehavior<IntegrationTestNotification>.ExecutionLog);
            Assert.Contains("After: IntegrationTestNotification", OpenGenericNotificationBehavior<IntegrationTestNotification>.ExecutionLog);
            Assert.Contains("Handled: OpenGenericTest", IntegrationNotificationHandler.ExecutionLog);
        }

        [Fact]
        public async Task IntegrationTest_WithClosedBehavior_ShouldExecuteBehavior()
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddCortexMediator(
                new[] { typeof(IntegrationNotificationHandler) },
                options =>
                {
                    options.AddNotificationPipelineBehavior<ClosedNotificationBehavior>();
                });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var notification = new IntegrationTestNotification { Data = "ClosedTest" };

            // Act
            ClosedNotificationBehavior.ExecutionLog.Clear();
            IntegrationNotificationHandler.ExecutionLog.Clear();
            await mediator.PublishAsync(notification);

            // Assert
            Assert.Contains("ClosedBehavior Before: ClosedTest", ClosedNotificationBehavior.ExecutionLog);
            Assert.Contains("ClosedBehavior After: ClosedTest", ClosedNotificationBehavior.ExecutionLog);
            Assert.Contains("Handled: ClosedTest", IntegrationNotificationHandler.ExecutionLog);
        }
    }
}
