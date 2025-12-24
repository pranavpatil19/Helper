using CoreBusiness;
using CoreBusiness.Validation;
using DataAccessLayer;
using DataAccessLayer.Exceptions;
using DataAccessLayer.Database.ECM.Interfaces;
using MigrationRunner.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Shared.Configuration;

namespace MigrationRunner;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMigrationEndpoints(
        this IServiceCollection services,
        MigrationRunnerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var source = options.Source
            ?? throw new DalConfigurationException("Source database configuration is missing.");
        var destination = options.Destination
            ?? throw new DalConfigurationException("Destination database configuration is missing.");

        return services.AddMigrationEndpoints(
            EndpointRegistration.FromOptions(source),
            EndpointRegistration.FromOptions(destination));
    }

    public static IServiceCollection AddMigrationEndpoints(
        this IServiceCollection services,
        EndpointRegistration source,
        EndpointRegistration destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        services.AddEndpoint(source, EndpointRuntimeOptionNames.Source, typeof(ISourceDbContextFactory));
        services.AddEndpoint(destination, EndpointRuntimeOptionNames.Destination, typeof(IDestinationDbContextFactory));
        return services;
    }

    public static IServiceCollection AddSourceServices(
        this IServiceCollection services,
        EndpointRegistration registration)
    {
        return services.AddEndpoint(registration, EndpointRuntimeOptionNames.Source, dbContextFactoryInterface: null);
    }

    public static IServiceCollection AddDestinationServices(
        this IServiceCollection services,
        EndpointRegistration registration)
    {
        return services.AddEndpoint(registration, EndpointRuntimeOptionNames.Destination, dbContextFactoryInterface: null);
    }

    public static IServiceCollection AddSourceDbContextFactory(
        this IServiceCollection services,
        EndpointRegistration registration)
    {
        return services.AddEndpoint(registration, EndpointRuntimeOptionNames.Source, typeof(ISourceDbContextFactory), includeServices: false);
    }

    public static IServiceCollection AddDestinationDbContextFactory(
        this IServiceCollection services,
        EndpointRegistration registration)
    {
        return services.AddEndpoint(registration, EndpointRuntimeOptionNames.Destination, typeof(IDestinationDbContextFactory), includeServices: false);
    }

    private static IServiceCollection AddEndpoint(
        this IServiceCollection services,
        EndpointRegistration registration,
        string runtimeOptionName,
        Type? dbContextFactoryInterface,
        bool includeServices = true)
    {
        if (includeServices)
        {
            services.AddDataAccessLayer(registration.Database);
            if (registration.IncludeEntityFramework)
            {
                services.AddEcmEntityFrameworkSupport(registration.Database);
            }
            services.AddCoreBusiness(registration.ConfigureValidation);
        }

        if (registration.IncludeEntityFramework && dbContextFactoryInterface is not null)
        {
            services.AddScoped(dbContextFactoryInterface, _ =>
                new EndpointDbContextFactory(registration.Database, registration.ConfigureDbContext));
        }

        services.ConfigureEndpointRuntimeOptions(runtimeOptionName, registration.Database);
        return services;
    }

    private static void ConfigureEndpointRuntimeOptions(
        this IServiceCollection services,
        string runtimeOptionName,
        DatabaseOptions databaseOptions)
    {
        services.AddOptions<EndpointRuntimeOptions>(runtimeOptionName)
            .Configure(options => options.Database = databaseOptions);
    }
}
