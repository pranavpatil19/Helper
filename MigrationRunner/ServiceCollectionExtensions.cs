using DataAccessLayer.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using MigrationRunner.Configuration;
using Shared.Configuration;

namespace MigrationRunner;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers source + destination providers plus the shared migration infrastructure using the raw configuration object.
    /// </summary>
    public static IServiceCollection AddMigrationRunnerServices(
        this IServiceCollection services,
        MigrationRunnerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var sourceRegistration = EndpointRegistration.FromOptions(
            options.Source ?? throw new DalConfigurationException("Source database configuration is missing."));
        var destinationRegistration = EndpointRegistration.FromOptions(
            options.Destination ?? throw new DalConfigurationException("Destination database configuration is missing."));

        return services.AddMigrationRunnerServices(sourceRegistration, destinationRegistration);
    }

    /// <summary>
    /// Registers source provider services, destination provider services, and the provider-agnostic migrator services.
    /// </summary>
    public static IServiceCollection AddMigrationRunnerServices(
        this IServiceCollection services,
        EndpointRegistration sourceRegistration,
        EndpointRegistration destinationRegistration)
    {
        ArgumentNullException.ThrowIfNull(sourceRegistration);
        ArgumentNullException.ThrowIfNull(destinationRegistration);

        services.RegisterSourceEndpoint(sourceRegistration);
        services.RegisterDestinationEndpoint(destinationRegistration);
        services.RegisterMigrationOrchestratorServices();

        return services;
    }
}
