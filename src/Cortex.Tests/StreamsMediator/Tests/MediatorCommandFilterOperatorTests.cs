using Cortex.Mediator;
using Cortex.Mediator.Commands;
using Cortex.Streams.Mediator.Operators;
using Cortex.Streams.Operators;
using Moq;

namespace Cortex.Tests.StreamsMediator.Tests
{
    #region Test Types

    public class ValidateOrderCommand : ICommand<ValidationResult>
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class OrderData
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string CustomerId { get; set; } = string.Empty;
    }

    #endregion

    public class MediatorCommandFilterOperatorTests
    {
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenMediatorIsNull()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorCommandFilterOperator<OrderData, ValidateOrderCommand, ValidationResult>(
                    null!,
                    _ => new ValidateOrderCommand(),
                    (_, result) => result.IsValid));
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenCommandFactoryIsNull()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorCommandFilterOperator<OrderData, ValidateOrderCommand, ValidationResult>(
                    mockMediator.Object,
                    null!,
                    (_, result) => result.IsValid));
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenFilterPredicateIsNull()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MediatorCommandFilterOperator<OrderData, ValidateOrderCommand, ValidationResult>(
                    mockMediator.Object,
                    _ => new ValidateOrderCommand(),
                    null!));
        }

        [Fact]
        public void Process_PassesItemDownstream_WhenPredicateReturnsTrue()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();

            mockMediator
                .Setup(m => m.SendCommandAsync<ValidateOrderCommand, ValidationResult>(
                    It.IsAny<ValidateOrderCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult { IsValid = true });

            var filterOperator = new MediatorCommandFilterOperator<OrderData, ValidateOrderCommand, ValidationResult>(
                mockMediator.Object,
                input => new ValidateOrderCommand { OrderId = input.OrderId, Amount = input.Amount },
                (input, result) => result.IsValid);

            filterOperator.SetNext(mockNextOperator.Object);

            var orderData = new OrderData { OrderId = "ORD-001", Amount = 100m, CustomerId = "CUST-001" };

            // Act
            filterOperator.Process(orderData);

            // Assert
            mockNextOperator.Verify(n => n.Process(
                It.Is<OrderData>(o => o.OrderId == "ORD-001")), 
                Times.Once);
        }

        [Fact]
        public void Process_FiltersOutItem_WhenPredicateReturnsFalse()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();

            mockMediator
                .Setup(m => m.SendCommandAsync<ValidateOrderCommand, ValidationResult>(
                    It.IsAny<ValidateOrderCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult { IsValid = false, ErrorMessage = "Invalid amount" });

            var filterOperator = new MediatorCommandFilterOperator<OrderData, ValidateOrderCommand, ValidationResult>(
                mockMediator.Object,
                input => new ValidateOrderCommand { OrderId = input.OrderId, Amount = input.Amount },
                (input, result) => result.IsValid);

            filterOperator.SetNext(mockNextOperator.Object);

            var orderData = new OrderData { OrderId = "ORD-002", Amount = -50m, CustomerId = "CUST-002" };

            // Act
            filterOperator.Process(orderData);

            // Assert
            mockNextOperator.Verify(n => n.Process(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public void Process_UsesInputAndResultInPredicate()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();

            mockMediator
                .Setup(m => m.SendCommandAsync<ValidateOrderCommand, ValidationResult>(
                    It.IsAny<ValidateOrderCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult { IsValid = true });

            bool predicateCalled = false;
            OrderData? predicateInput = null;
            ValidationResult? predicateResult = null;

            var filterOperator = new MediatorCommandFilterOperator<OrderData, ValidateOrderCommand, ValidationResult>(
                mockMediator.Object,
                input => new ValidateOrderCommand { OrderId = input.OrderId },
                (input, result) =>
                {
                    predicateCalled = true;
                    predicateInput = input;
                    predicateResult = result;
                    return result.IsValid && input.Amount > 50;
                });

            filterOperator.SetNext(mockNextOperator.Object);

            var orderData = new OrderData { OrderId = "ORD-003", Amount = 100m };

            // Act
            filterOperator.Process(orderData);

            // Assert
            Assert.True(predicateCalled);
            Assert.NotNull(predicateInput);
            Assert.Equal("ORD-003", predicateInput.OrderId);
            Assert.NotNull(predicateResult);
            Assert.True(predicateResult.IsValid);
            mockNextOperator.Verify(n => n.Process(It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public void Process_FiltersOutItem_WhenExceptionOccursAndPassOnErrorIsFalse()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();
            Exception? capturedException = null;

            mockMediator
                .Setup(m => m.SendCommandAsync<ValidateOrderCommand, ValidationResult>(
                    It.IsAny<ValidateOrderCommand>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Validation service unavailable"));

            var filterOperator = new MediatorCommandFilterOperator<OrderData, ValidateOrderCommand, ValidationResult>(
                mockMediator.Object,
                input => new ValidateOrderCommand { OrderId = input.OrderId },
                (_, result) => result.IsValid,
                errorHandler: (_, ex) => capturedException = ex,
                passOnError: false);

            filterOperator.SetNext(mockNextOperator.Object);

            // Act
            filterOperator.Process(new OrderData { OrderId = "ORD-004" });

            // Assert
            Assert.NotNull(capturedException);
            mockNextOperator.Verify(n => n.Process(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public void Process_PassesItemDownstream_WhenExceptionOccursAndPassOnErrorIsTrue()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();

            mockMediator
                .Setup(m => m.SendCommandAsync<ValidateOrderCommand, ValidationResult>(
                    It.IsAny<ValidateOrderCommand>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Validation service unavailable"));

            var filterOperator = new MediatorCommandFilterOperator<OrderData, ValidateOrderCommand, ValidationResult>(
                mockMediator.Object,
                input => new ValidateOrderCommand { OrderId = input.OrderId },
                (_, result) => result.IsValid,
                errorHandler: (_, _) => { },
                passOnError: true);

            filterOperator.SetNext(mockNextOperator.Object);

            var orderData = new OrderData { OrderId = "ORD-005" };

            // Act
            filterOperator.Process(orderData);

            // Assert
            mockNextOperator.Verify(n => n.Process(
                It.Is<OrderData>(o => o.OrderId == "ORD-005")), 
                Times.Once);
        }

        [Fact]
        public void Process_InvokesErrorHandler_WhenExceptionOccurs()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();
            OrderData? capturedInput = null;
            Exception? capturedException = null;

            mockMediator
                .Setup(m => m.SendCommandAsync<ValidateOrderCommand, ValidationResult>(
                    It.IsAny<ValidateOrderCommand>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test error"));

            var filterOperator = new MediatorCommandFilterOperator<OrderData, ValidateOrderCommand, ValidationResult>(
                mockMediator.Object,
                input => new ValidateOrderCommand { OrderId = input.OrderId },
                (_, result) => result.IsValid,
                errorHandler: (input, ex) =>
                {
                    capturedInput = input;
                    capturedException = ex;
                });

            filterOperator.SetNext(mockNextOperator.Object);

            var orderData = new OrderData { OrderId = "ORD-006" };

            // Act
            filterOperator.Process(orderData);

            // Assert
            Assert.NotNull(capturedInput);
            Assert.Equal("ORD-006", capturedInput.OrderId);
            Assert.NotNull(capturedException);
            Assert.IsType<InvalidOperationException>(capturedException);
        }

        [Fact]
        public void SetNext_SetsNextOperator()
        {
            // Arrange
            var mockMediator = new Mock<IMediator>();
            var mockNextOperator = new Mock<IOperator>();

            mockMediator
                .Setup(m => m.SendCommandAsync<ValidateOrderCommand, ValidationResult>(
                    It.IsAny<ValidateOrderCommand>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult { IsValid = true });

            var filterOperator = new MediatorCommandFilterOperator<OrderData, ValidateOrderCommand, ValidationResult>(
                mockMediator.Object,
                input => new ValidateOrderCommand { OrderId = input.OrderId },
                (_, result) => result.IsValid);

            // Act
            filterOperator.SetNext(mockNextOperator.Object);
            filterOperator.Process(new OrderData { OrderId = "TEST" });

            // Assert
            mockNextOperator.Verify(n => n.Process(It.IsAny<object>()), Times.Once);
        }
    }
}
