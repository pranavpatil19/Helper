using System;
using CoreBusiness.Services;
using CoreBusiness.Validation;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreBusiness;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreBusiness(
        this IServiceCollection services,
        Action<ValidationOptions>? configureValidation = null)
    {
        services.AddScoped<ITodoService, TodoService>();
        services.AddScoped<IValidationService, ValidationService>();

        var optionsBuilder = services.AddOptions<ValidationOptions>();
        if (configureValidation is null)
        {
            optionsBuilder.Configure(options => options.Enabled = true);
        }
        else
        {
            optionsBuilder.Configure(configureValidation);
        }

        services.AddValidatorsFromAssemblyContaining<TodoCreateRequestValidator>();
        return services;
    }

    public static IServiceCollection AddCoreBusiness(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddCoreBusiness(options =>
            configuration.GetSection(ValidationOptions.SectionName).Bind(options));
    }
}
