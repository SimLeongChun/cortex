using Cortex.Streams.Operators;
using System.Collections.Concurrent;

namespace Cortex.Streams.Tests
{
    /// <summary>
    /// Integration tests for the FanOut feature simulating real-world scenarios.
    /// </summary>
    public class FanOutIntegrationTests
    {
        #region E-Commerce Order Processing Scenario

        [Fact]
        public void FanOut_OrderProcessing_DistributesToMultipleDestinations()
        {
            // Arrange - Simulate an e-commerce order processing pipeline
            var databaseOrders = new List<Order>();
            var kafkaEvents = new List<OrderEvent>();
            var highValueAlerts = new List<Order>();
            var analyticsData = new List<OrderMetrics>();

            var stream = StreamBuilder<Order>
                .CreateNewStream("OrderProcessingPipeline")
                .Stream()
                .Filter(order => order.Status == OrderStatus.Confirmed)
                .FanOut(fanOut => fanOut
                    // Persist all confirmed orders to database
                    .To("database", order => databaseOrders.Add(order))
                    // Publish order events to Kafka for downstream consumers
                    .ToWithTransform("kafka-events",
                        order => new OrderEvent(order.Id, "OrderConfirmed", DateTime.UtcNow),
                        evt => kafkaEvents.Add(evt))
                    // Alert on high-value orders (> $1000)
                    .To("high-value-alerts",
                        order => order.TotalAmount > 1000,
                        order => highValueAlerts.Add(order))
                    // Send metrics for analytics
                    .ToWithTransform("analytics",
                        order => new OrderMetrics(order.Id, order.TotalAmount, order.Items.Length),
                        metrics => analyticsData.Add(metrics)))
                .Build();

            stream.Start();

            // Act - Process various orders
            var order1 = new Order("ORD-001", OrderStatus.Confirmed, 500, new[] { "Item1", "Item2" });
            var order2 = new Order("ORD-002", OrderStatus.Pending, 2000, new[] { "Item3" }); // Filtered out
            var order3 = new Order("ORD-003", OrderStatus.Confirmed, 1500, new[] { "Item4", "Item5", "Item6" });
            var order4 = new Order("ORD-004", OrderStatus.Confirmed, 250, new[] { "Item7" });

            stream.Emit(order1);
            stream.Emit(order2);
            stream.Emit(order3);
            stream.Emit(order4);

            // Assert
            Assert.Equal(3, databaseOrders.Count); // 3 confirmed orders
            Assert.Equal(3, kafkaEvents.Count);
            Assert.Single(highValueAlerts); // Only order3 > $1000
            Assert.Equal("ORD-003", highValueAlerts[0].Id);
            Assert.Equal(3, analyticsData.Count);
            Assert.Equal(3, analyticsData[1].ItemCount); // order3 has 3 items
        }

        #endregion

        #region IoT Sensor Data Scenario

        [Fact]
        public void FanOut_SensorData_RoutesToDifferentStorageByThreshold()
        {
            // Arrange - IoT sensor data processing
            var allReadings = new ConcurrentBag<SensorReading>();
            var criticalAlerts = new ConcurrentBag<SensorReading>();
            var warningLog = new ConcurrentBag<SensorReading>();
            var normalArchive = new ConcurrentBag<SensorReading>();

            var stream = StreamBuilder<SensorReading>
                .CreateNewStream("SensorDataPipeline")
                .Stream()
                .FanOut(fanOut => fanOut
                    // Archive all readings
                    .To("archive", reading => allReadings.Add(reading))
                    // Critical: Temperature > 100°C
                    .To("critical-alerts",
                        reading => reading.Temperature > 100,
                        reading => criticalAlerts.Add(reading))
                    // Warning: Temperature between 80-100°C
                    .To("warning-log",
                        reading => reading.Temperature >= 80 && reading.Temperature <= 100,
                        reading => warningLog.Add(reading))
                    // Normal: Temperature < 80°C
                    .To("normal-archive",
                        reading => reading.Temperature < 80,
                        reading => normalArchive.Add(reading)))
                .Build();

            stream.Start();

            // Act - Emit sensor readings
            var readings = new[]
            {
                new SensorReading("Sensor-1", 65.5, DateTime.UtcNow),
                new SensorReading("Sensor-2", 85.0, DateTime.UtcNow),
                new SensorReading("Sensor-3", 105.2, DateTime.UtcNow),
                new SensorReading("Sensor-1", 72.0, DateTime.UtcNow),
                new SensorReading("Sensor-2", 95.5, DateTime.UtcNow),
            };

            foreach (var reading in readings)
            {
                stream.Emit(reading);
            }

            // Assert
            Assert.Equal(5, allReadings.Count);
            Assert.Single(criticalAlerts);
            Assert.Equal(2, warningLog.Count);
            Assert.Equal(2, normalArchive.Count);
        }

        #endregion

        #region User Activity Tracking Scenario

        [Fact]
        public void FanOut_UserActivity_SendsToMultipleAnalyticsSystems()
        {
            // Arrange - User activity tracking for analytics
            var clickstreamData = new List<UserActivity>();
            var purchaseEvents = new List<UserActivity>();
            var searchQueries = new List<string>();
            var sessionMetrics = new List<SessionMetric>();

            var stream = StreamBuilder<UserActivity>
                .CreateNewStream("UserActivityPipeline")
                .Stream()
                .FanOut(fanOut => fanOut
                    // All clickstream to data lake
                    .To("clickstream", activity => clickstreamData.Add(activity))
                    // Purchase events to commerce analytics
                    .To("purchases",
                        activity => activity.Type == ActivityType.Purchase,
                        activity => purchaseEvents.Add(activity))
                    // Search queries for search optimization
                    .To("search-queries",
                        activity => activity.Type == ActivityType.Search,
                        activity => searchQueries.Add(activity.Details))
                    // Session metrics for engagement analysis
                    .ToWithTransform("session-metrics",
                        activity => new SessionMetric(activity.UserId, activity.SessionId, activity.Timestamp),
                        metric => sessionMetrics.Add(metric)))
                .Build();

            stream.Start();

            // Act
            var activities = new[]
            {
                new UserActivity("user-1", "sess-1", ActivityType.PageView, "Home", DateTime.UtcNow),
                new UserActivity("user-1", "sess-1", ActivityType.Search, "laptop", DateTime.UtcNow),
                new UserActivity("user-1", "sess-1", ActivityType.Purchase, "laptop-123", DateTime.UtcNow),
                new UserActivity("user-2", "sess-2", ActivityType.PageView, "Products", DateTime.UtcNow),
                new UserActivity("user-2", "sess-2", ActivityType.Search, "headphones", DateTime.UtcNow),
            };

            foreach (var activity in activities)
            {
                stream.Emit(activity);
            }

            // Assert
            Assert.Equal(5, clickstreamData.Count);
            Assert.Single(purchaseEvents);
            Assert.Equal(2, searchQueries.Count);
            Assert.Contains("laptop", searchQueries);
            Assert.Contains("headphones", searchQueries);
            Assert.Equal(5, sessionMetrics.Count);
        }

        #endregion

        #region Log Aggregation Scenario

        [Fact]
        public void FanOut_LogAggregation_RoutesLogsByLevel()
        {
            // Arrange - Log aggregation pipeline
            var allLogs = new List<LogEntry>();
            var errorLogs = new List<LogEntry>();
            var warningLogs = new List<LogEntry>();
            var metricsData = new List<LogMetric>();

            var stream = StreamBuilder<LogEntry>
                .CreateNewStream("LogAggregationPipeline")
                .Stream()
                .FanOut(fanOut => fanOut
                    // All logs to Elasticsearch
                    .To("elasticsearch", log => allLogs.Add(log))
                    // Errors to PagerDuty
                    .To("pagerduty",
                        log => log.Level == LogLevel.Error,
                        log => errorLogs.Add(log))
                    // Warnings to Slack
                    .To("slack",
                        log => log.Level == LogLevel.Warning,
                        log => warningLogs.Add(log))
                    // Metrics to Prometheus
                    .ToWithTransform("prometheus",
                        log => new LogMetric(log.Service, log.Level.ToString(), 1),
                        metric => metricsData.Add(metric)))
                .Build();

            stream.Start();

            // Act
            var logs = new[]
            {
                new LogEntry("api-gateway", LogLevel.Info, "Request received"),
                new LogEntry("user-service", LogLevel.Error, "Database connection failed"),
                new LogEntry("order-service", LogLevel.Warning, "High latency detected"),
                new LogEntry("api-gateway", LogLevel.Info, "Response sent"),
                new LogEntry("user-service", LogLevel.Error, "Retry failed"),
            };

            foreach (var log in logs)
            {
                stream.Emit(log);
            }

            // Assert
            Assert.Equal(5, allLogs.Count);
            Assert.Equal(2, errorLogs.Count);
            Assert.Single(warningLogs);
            Assert.Equal(5, metricsData.Count);
        }

        #endregion

        #region Financial Transaction Scenario

        [Fact]
        public void FanOut_FinancialTransactions_MultipleComplianceChecks()
        {
            // Arrange - Financial transaction processing with compliance
            var ledger = new List<Transaction>();
            var fraudAlerts = new List<Transaction>();
            var largeTransactionReports = new List<Transaction>();
            var auditLog = new List<AuditEntry>();

            var stream = StreamBuilder<Transaction>
                .CreateNewStream("FinancialPipeline")
                .Stream()
                .FanOut(fanOut => fanOut
                    // Main ledger
                    .To("ledger", txn => ledger.Add(txn))
                    // Fraud detection (unusual patterns)
                    .To("fraud-detection",
                        txn => txn.Amount > 10000 && txn.Type == TransactionType.International,
                        txn => fraudAlerts.Add(txn))
                    // Regulatory reporting (> $10,000)
                    .To("regulatory-reporting",
                        txn => txn.Amount > 10000,
                        txn => largeTransactionReports.Add(txn))
                    // Audit trail
                    .ToWithTransform("audit",
                        txn => new AuditEntry(txn.Id, "PROCESSED", DateTime.UtcNow),
                        entry => auditLog.Add(entry)))
                .Build();

            stream.Start();

            // Act
            var transactions = new[]
            {
                new Transaction("TXN-001", 500, TransactionType.Domestic),
                new Transaction("TXN-002", 15000, TransactionType.International), // Fraud + Large
                new Transaction("TXN-003", 25000, TransactionType.Domestic),       // Large only
                new Transaction("TXN-004", 8000, TransactionType.International),
            };

            foreach (var txn in transactions)
            {
                stream.Emit(txn);
            }

            // Assert
            Assert.Equal(4, ledger.Count);
            Assert.Single(fraudAlerts); // Only TXN-002 (international + >10k)
            Assert.Equal(2, largeTransactionReports.Count); // TXN-002 and TXN-003
            Assert.Equal(4, auditLog.Count);
        }

        #endregion

        #region Test Models

        private record Order(string Id, OrderStatus Status, decimal TotalAmount, string[] Items);
        private record OrderEvent(string OrderId, string EventType, DateTime Timestamp);
        private record OrderMetrics(string OrderId, decimal Amount, int ItemCount);

        private enum OrderStatus { Pending, Confirmed, Shipped, Delivered }

        private record SensorReading(string SensorId, double Temperature, DateTime Timestamp);

        private record UserActivity(string UserId, string SessionId, ActivityType Type, string Details, DateTime Timestamp);
        private record SessionMetric(string UserId, string SessionId, DateTime Timestamp);

        private enum ActivityType { PageView, Search, Purchase, AddToCart }

        private record LogEntry(string Service, LogLevel Level, string Message);
        private record LogMetric(string Service, string Level, int Count);

        private enum LogLevel { Debug, Info, Warning, Error }

        private record Transaction(string Id, decimal Amount, TransactionType Type);
        private record AuditEntry(string TransactionId, string Status, DateTime Timestamp);

        private enum TransactionType { Domestic, International }

        #endregion
    }
}
