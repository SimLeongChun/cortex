using Cortex.Mediator.Behaviors;
using Cortex.Mediator.Commands;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace Cortex.Tests.Mediator.FluentValidation.Tests
{
    public class ValidationCommandBehaviorTests
    {
        public class FakeCommand : ICommand<string>
        {
        }

        [Fact]
        public async Task Handle_ShouldNotThrowExceptionsWhenThereAreNoValidationFailures()
        {
            // Arrange
            var expectedResult = "completed";
            var validator = new Mock<IValidator<FakeCommand>>();
            validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), default)).Returns(() => Task.FromResult(new ValidationResult { Errors = new List<ValidationFailure>()}));
            var validators = new IValidator<FakeCommand>[]
            {
                    validator.Object,
            };

            var next = new Mock<CommandHandlerDelegate<string>>();
            next.Setup(n => n.Invoke()).Returns(Task.FromResult(expectedResult));
            var systemUnderTest = new ValidationCommandBehavior<FakeCommand, string>(validators);

            // Act

            var result = await systemUnderTest.Handle(new FakeCommand(), next.Object, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task Handle_ShouldThrowAnExceptionsWhenThereIsAValidationFailure()
        {
            // Arrange
            var expectedResult = "completed";
            var validator = new Mock<IValidator<FakeCommand>>();
            validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), default))
                .Returns(() => Task.FromResult(new ValidationResult 
                { 
                    Errors = new List<ValidationFailure> 
                    { 
                        new ValidationFailure("some-property", "was invalid")
                    } 
                }
            ));

            var validators = new IValidator<FakeCommand>[]
            {
                    validator.Object,
            };

            var next = new Mock<CommandHandlerDelegate<string>>();
            next.Setup(n => n.Invoke()).Returns(Task.FromResult(expectedResult));
            var systemUnderTest = new ValidationCommandBehavior<FakeCommand, string>(validators);

            // Act
            // Assert
            await Assert.ThrowsAsync<Cortex.Mediator.Exceptions.ValidationException>(async () => await systemUnderTest.Handle(new FakeCommand(), next.Object, CancellationToken.None));
        }
    }
}
