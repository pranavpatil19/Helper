using System;
using CoreBusiness.Validation;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreBusiness;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core business services and validators.
    /// </summary>
    public static IServiceCollection AddCoreBusiness(
        this IServiceCollection services,
        Action<ValidationOptions>? configureValidation = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IValidationService, ValidationService>();
        services.AddValidatorsFromAssemblyContaining<ValidationService>();

        ConfigureValidationPipeline(services, configureValidation);
        return services;
    }

    /// <summary>
    /// Registers the core business services and binds validation options from configuration.
    /// </summary>
    public static IServiceCollection AddCoreBusiness(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddCoreBusiness(options =>
            configuration.GetSection(ValidationOptions.SectionName).Bind(options));
    }
}
