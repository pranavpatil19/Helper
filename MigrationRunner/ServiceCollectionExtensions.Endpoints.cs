using CoreBusiness;
using DataAccessLayer;
using DataAccessLayer.Database.ECM.DbContexts;
using Microsoft.Extensions.DependencyInjection;
using MigrationRunner.Configuration;
using Shared.Configuration;

namespace MigrationRunner;

public static partial class ServiceCollectionExtensions
{
    private static void RegisterSourceEndpoint(this IServiceCollection services, EndpointRegistration registration)
    {
        services.RegisterEndpoint(
            registration,
            EndpointRuntimeOptionNames.Source,
            typeof(ISourceDbContextFactory));
    }

    private static void RegisterDestinationEndpoint(this IServiceCollection services, EndpointRegistration registration)
    {
        services.RegisterEndpoint(
            registration,
            EndpointRuntimeOptionNames.Destination,
            typeof(IDestinationDbContextFactory));
    }

    private static void RegisterEndpoint(
        this IServiceCollection services,
        EndpointRegistration registration,
        string runtimeOptionName,
        Type dbContextFactoryInterface)
    {
        services.AddDataAccessLayer(registration.Database);
        services.AddCoreBusiness(registration.ConfigureValidation);
        // Wire the provider-specific business workflow explicitly so callers can see which implementation is active.
        services.AddMigrationWorkflow(registration.Database);

        if (registration.IncludeEntityFramework)
        {
            services.AddEcmEntityFrameworkSupport(registration.Database);
            // DbContext factories are scoped so they respect the host's scoped lifetime and can resolve per-request options.
            services.AddScoped(dbContextFactoryInterface, _ =>
                new EndpointDbContextFactory(registration.Database, registration.ConfigureDbContext));
        }

        services.AddOptions<EndpointRuntimeOptions>(runtimeOptionName)
            .Configure(options => options.Database = registration.Database);
    }
}
