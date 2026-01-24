using Cortex.Mediator.Commands;
using FluentValidation;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cortex.Mediator.Behaviors
{
    /// <summary>
    /// Pipeline behavior for validation command execution.
    /// </summary>
    public sealed class ValidationCommandBehavior<TCommand, TResult> : ICommandPipelineBehavior<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        private readonly IEnumerable<IValidator<TCommand>> _validators;

        public ValidationCommandBehavior(IEnumerable<IValidator<TCommand>> validators)
        {
            _validators = validators;
        }

        public async Task<TResult> Handle(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken cancellationToken)
        {
            var context = new ValidationContext<TCommand>(command);
            var failures = _validators
                .Select(async v => await v.ValidateAsync(context))
                .Select(r => r.Result)
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Count() > 0)
            {
                var errors = failures
                    .GroupBy(f => f.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(f => f.ErrorMessage).ToArray());

                throw new Exceptions.ValidationException(errors);
            }

            return await next();
        }
    }
}
