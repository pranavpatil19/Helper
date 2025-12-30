using System;
using CoreBusiness.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace CoreBusiness;

public static partial class ServiceCollectionExtensions
{
    private static void ConfigureValidationPipeline(
        IServiceCollection services,
        Action<ValidationOptions>? configureValidation)
    {
        var optionsBuilder = services.AddOptions<ValidationOptions>();
        if (configureValidation is null)
        {
            optionsBuilder.Configure(options => options.Enabled = true);
            return;
        }

        optionsBuilder.Configure(configureValidation);
    }
}
