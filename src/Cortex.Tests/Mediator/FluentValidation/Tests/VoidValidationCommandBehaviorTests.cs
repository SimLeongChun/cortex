using Cortex.Mediator.Behaviors;
using Cortex.Mediator.Commands;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace Cortex.Tests.Mediator.FluentValidation.Tests
{
    public class FakeVoidCommand : ICommand
    {
    }

    public class VoidValidationCommandBehaviorTests
    {
        [Fact]
        public async Task Handle_ShouldNotThrowExceptionsWhenThereAreNoValidationFailures()
        {
            // Arrange
            var expectedResult = "completed";
            var validator = new Mock<IValidator<FakeVoidCommand>>();
            validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), default)).Returns(() => Task.FromResult(new ValidationResult { Errors = new List<ValidationFailure>()}));
            var validators = new IValidator<FakeVoidCommand>[]
            {
                    validator.Object,
            };

            var next = new Mock<CommandHandlerDelegate>();
            next.Setup(n => n.Invoke()).Returns(Task.FromResult(expectedResult));
            var systemUnderTest = new ValidationCommandBehavior<FakeVoidCommand>(validators);

            // Act 
            // Assert
            await systemUnderTest.Handle(new FakeVoidCommand(), next.Object, CancellationToken.None);
        }

        [Fact]
        public async Task Handle_ShouldThrowAnExceptionsWhenThereIsAValidationFailure()
        {
            // Arrange
            var expectedResult = "completed";
            var validator = new Mock<IValidator<FakeVoidCommand>>();
            validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), default))
                .Returns(() => Task.FromResult(new ValidationResult 
                { 
                    Errors = new List<ValidationFailure> 
                    { 
                        new ValidationFailure("some-property", "was invalid")
                    } 
                }
            ));

            var validators = new IValidator<FakeVoidCommand>[]
            {
                    validator.Object,
            };

            var next = new Mock<CommandHandlerDelegate>();
            next.Setup(n => n.Invoke()).Returns(Task.FromResult(expectedResult));
            var systemUnderTest = new ValidationCommandBehavior<FakeVoidCommand>(validators);

            // Act
            // Assert
            await Assert.ThrowsAsync<Cortex.Mediator.Exceptions.ValidationException>(async () => await systemUnderTest.Handle(new FakeVoidCommand(), next.Object, CancellationToken.None));
        }
    }
}
