using Cortex.Mediator;
using Cortex.Mediator.Queries;
using Cortex.Streams.Mediator.Operators;
using Cortex.Streams.Operators;
using Moq;

namespace Cortex.Tests.StreamsMediator.Tests
{
    #region Test Queries

    public class GetProductDetailsQuery : IQuery<ProductDetails>
    {
        public string ProductId { get; set; } = string.Empty;
    }

    public class ProductDetails
    {
        public string ProductId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class GetDiscountQuery : IQuery<decimal>
    {
        public string CustomerId { get; set; } = string.Empty;
    }

    #endregion

    #region Test Input Types

    public class OrderLineItem
    {
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class EnrichedOrderLineItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice => UnitPrice * Quantity;
    }

    #endregion

    public class MediatorQueryMapOperatorTests
    {
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenMediatorIsNull()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorQueryMapOperator<OrderLineItem, GetProductDetailsQuery, ProductDetails>(
                    null!,
                    _ => new GetProductDetailsQuery()));
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenQueryFactoryIsNull()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorQueryMapOperator<OrderLineItem, GetProductDetailsQuery, ProductDetails>(
                    mockMediator.Object,
                    null!));
        }

        [Fact]
        public void Process_ExecutesQueryAndPassesResultDownstream()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();
            var expectedProductDetails = new ProductDetails 
            { 
                ProductId = "PROD-001", 
                Name = "Widget", 
                Price = 29.99m 
            };

            mockMediator
                .Setup(m => m.SendQueryAsync<GetProductDetailsQuery, ProductDetails>(
                    It.IsAny<GetProductDetailsQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedProductDetails);

            var mapOperator = new MediatorQueryMapOperator<OrderLineItem, GetProductDetailsQuery, ProductDetails>(
                mockMediator.Object,
                input => new GetProductDetailsQuery { ProductId = input.ProductId });

            mapOperator.SetNext(mockNextOperator.Object);

            var input = new OrderLineItem { ProductId = "PROD-001", Quantity = 5 };

            // Act
            mapOperator.Process(input);

            // Assert
            mockMediator.Verify(m => m.SendQueryAsync<GetProductDetailsQuery, ProductDetails>(
                It.Is<GetProductDetailsQuery>(q => q.ProductId == "PROD-001"),
                It.IsAny<CancellationToken>()), Times.Once);

            mockNextOperator.Verify(n => n.Process(
                It.Is<ProductDetails>(p => p.ProductId == "PROD-001" && p.Name == "Widget")), 
                Times.Once);
        }

        [Fact]
        public void Process_AppliesResultProjector_WhenProvided()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();
            var productDetails = new ProductDetails 
            { 
                ProductId = "PROD-002", 
                Name = "Gadget", 
                Price = 49.99m 
            };

            mockMediator
                .Setup(m => m.SendQueryAsync<GetProductDetailsQuery, ProductDetails>(
                    It.IsAny<GetProductDetailsQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(productDetails);

            var mapOperator = new MediatorQueryMapOperator<OrderLineItem, GetProductDetailsQuery, ProductDetails>(
                mockMediator.Object,
                input => new GetProductDetailsQuery { ProductId = input.ProductId },
                resultProjector: (input, result) => new ProductDetails
                {
                    ProductId = result.ProductId,
                    Name = $"{result.Name} (x{input.Quantity})",
                    Price = result.Price * input.Quantity
                });

            mapOperator.SetNext(mockNextOperator.Object);

            var input = new OrderLineItem { ProductId = "PROD-002", Quantity = 3 };

            // Act
            mapOperator.Process(input);

            // Assert
            mockNextOperator.Verify(n => n.Process(
                It.Is<ProductDetails>(p => 
                    p.Name == "Gadget (x3)" && 
                    p.Price == 149.97m)), 
                Times.Once);
        }

        [Fact]
        public void Process_InvokesErrorHandler_WhenQueryFails()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            Exception? capturedException = null;
            OrderLineItem? capturedInput = null;

            mockMediator
                .Setup(m => m.SendQueryAsync<GetProductDetailsQuery, ProductDetails>(
                    It.IsAny<GetProductDetailsQuery>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Query failed"));

            var mapOperator = new MediatorQueryMapOperator<OrderLineItem, GetProductDetailsQuery, ProductDetails>(
                mockMediator.Object,
                input => new GetProductDetailsQuery { ProductId = input.ProductId },
                errorHandler: (input, ex) =>
                {
                    capturedInput = input;
                    capturedException = ex;
                });

            var input = new OrderLineItem { ProductId = "PROD-003", Quantity = 1 };

            // Act
            mapOperator.Process(input);

            // Assert
            Assert.NotNull(capturedInput);
            Assert.Equal("PROD-003", capturedInput.ProductId);
            Assert.NotNull(capturedException);
            Assert.IsType<InvalidOperationException>(capturedException);
        }

        [Fact]
        public void Process_ThrowsException_WhenNoErrorHandlerProvided()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();

            mockMediator
                .Setup(m => m.SendQueryAsync<GetProductDetailsQuery, ProductDetails>(
                    It.IsAny<GetProductDetailsQuery>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Query failed"));

            var mapOperator = new MediatorQueryMapOperator<OrderLineItem, GetProductDetailsQuery, ProductDetails>(
                mockMediator.Object,
                input => new GetProductDetailsQuery { ProductId = input.ProductId });

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                mapOperator.Process(new OrderLineItem { ProductId = "PROD-004" }));
        }

        [Fact]
        public void SetNext_SetsNextOperator()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();
            var productDetails = new ProductDetails { ProductId = "TEST" };

            mockMediator
                .Setup(m => m.SendQueryAsync<GetProductDetailsQuery, ProductDetails>(
                    It.IsAny<GetProductDetailsQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(productDetails);

            var mapOperator = new MediatorQueryMapOperator<OrderLineItem, GetProductDetailsQuery, ProductDetails>(
                mockMediator.Object,
                input => new GetProductDetailsQuery { ProductId = input.ProductId });

            // Act
            mapOperator.SetNext(mockNextOperator.Object);
            mapOperator.Process(new OrderLineItem { ProductId = "TEST" });

            // Assert
            mockNextOperator.Verify(n => n.Process(It.IsAny<object>()), Times.Once);
        }
    }

    public class MediatorQueryEnrichOperatorTests
    {
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenMediatorIsNull()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorQueryEnrichOperator<OrderLineItem, GetProductDetailsQuery, ProductDetails, EnrichedOrderLineItem>(
                    null!,
                    _ => new GetProductDetailsQuery(),
                    (input, result) => new EnrichedOrderLineItem()));
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenEnricherIsNull()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorQueryEnrichOperator<OrderLineItem, GetProductDetailsQuery, ProductDetails, EnrichedOrderLineItem>(
                    mockMediator.Object,
                    _ => new GetProductDetailsQuery(),
                    null!));
        }

        [Fact]
        public void Process_EnrichesInputWithQueryResult()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();
            var productDetails = new ProductDetails 
            { 
                ProductId = "PROD-001", 
                Name = "Super Widget", 
                Price = 19.99m 
            };

            mockMediator
                .Setup(m => m.SendQueryAsync<GetProductDetailsQuery, ProductDetails>(
                    It.IsAny<GetProductDetailsQuery>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(productDetails);

            var enrichOperator = new MediatorQueryEnrichOperator<OrderLineItem, GetProductDetailsQuery, ProductDetails, EnrichedOrderLineItem>(
                mockMediator.Object,
                input => new GetProductDetailsQuery { ProductId = input.ProductId },
                enricher: (input, result) => new EnrichedOrderLineItem
                {
                    ProductId = input.ProductId,
                    ProductName = result.Name,
                    UnitPrice = result.Price,
                    Quantity = input.Quantity
                });

            enrichOperator.SetNext(mockNextOperator.Object);

            var input = new OrderLineItem { ProductId = "PROD-001", Quantity = 3 };

            // Act
            enrichOperator.Process(input);

            // Assert
            mockNextOperator.Verify(n => n.Process(
                It.Is<EnrichedOrderLineItem>(e => 
                    e.ProductId == "PROD-001" && 
                    e.ProductName == "Super Widget" && 
                    e.UnitPrice == 19.99m && 
                    e.Quantity == 3)), 
                Times.Once);
        }

        [Fact]
        public void Process_SkipsItem_WhenErrorOccursAndSkipOnErrorIsTrue()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();

            mockMediator
                .Setup(m => m.SendQueryAsync<GetProductDetailsQuery, ProductDetails>(
                    It.IsAny<GetProductDetailsQuery>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Query failed"));

            var enrichOperator = new MediatorQueryEnrichOperator<OrderLineItem, GetProductDetailsQuery, ProductDetails, EnrichedOrderLineItem>(
                mockMediator.Object,
                input => new GetProductDetailsQuery { ProductId = input.ProductId },
                enricher: (input, result) => new EnrichedOrderLineItem(),
                skipOnError: true);

            enrichOperator.SetNext(mockNextOperator.Object);

            // Act
            enrichOperator.Process(new OrderLineItem { ProductId = "PROD-FAIL" });

            // Assert
            mockNextOperator.Verify(n => n.Process(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public void Process_UsesDefaultOutput_WhenErrorOccursAndSkipOnErrorIsFalse()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();
            var defaultOutput = new EnrichedOrderLineItem 
            { 
                ProductId = "DEFAULT", 
                ProductName = "Unknown", 
                UnitPrice = 0m, 
                Quantity = 0 
            };

            mockMediator
                .Setup(m => m.SendQueryAsync<GetProductDetailsQuery, ProductDetails>(
                    It.IsAny<GetProductDetailsQuery>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Query failed"));

            var enrichOperator = new MediatorQueryEnrichOperator<OrderLineItem, GetProductDetailsQuery, ProductDetails, EnrichedOrderLineItem>(
                mockMediator.Object,
                input => new GetProductDetailsQuery { ProductId = input.ProductId },
                enricher: (input, result) => new EnrichedOrderLineItem(),
                defaultOutput: defaultOutput,
                skipOnError: false);

            enrichOperator.SetNext(mockNextOperator.Object);

            // Act
            enrichOperator.Process(new OrderLineItem { ProductId = "PROD-FAIL" });

            // Assert
            mockNextOperator.Verify(n => n.Process(
                It.Is<EnrichedOrderLineItem>(e => e.ProductId == "DEFAULT")), 
                Times.Once);
        }
    }
}
