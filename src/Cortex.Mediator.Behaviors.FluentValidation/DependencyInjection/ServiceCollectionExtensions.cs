using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace Cortex.Mediator.Behaviors.FluentValidation.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFluentValidationValidators(this IServiceCollection services, Type[] validationAssemblyMarkerTypes)
        {
            services.AddValidatorsFromAssemblies(validationAssemblyMarkerTypes.Select(t => t.Assembly));
            return services;
        }
    }
}
