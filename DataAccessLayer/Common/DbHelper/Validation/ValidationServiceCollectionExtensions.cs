using DataAccessLayer.Execution;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;

namespace DataAccessLayer.Validation;

internal static class ValidationServiceCollectionExtensions
{
    public static void AddDalValidation(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<DbParameterDefinition>>(sp =>
            new DbParameterDefinitionValidator(sp.GetRequiredService<DatabaseOptions>().InputNormalization));
        services.AddSingleton<IValidator<DbCommandRequest>>(sp =>
            new DbCommandRequestValidator(sp.GetRequiredService<IValidator<DbParameterDefinition>>()));
    }
}
