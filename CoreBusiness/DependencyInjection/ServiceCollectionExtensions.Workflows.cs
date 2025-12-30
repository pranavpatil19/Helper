using System;
using CoreBusiness.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;

namespace CoreBusiness;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the provider-specific <see cref="IMigrationWorkflow"/> implementation.
    /// </summary>
    /// <param name="provider">Active provider that decides which workflow is wired up.</param>
    /// <param name="lifetime">Controls the lifestyle for the workflow (Scoped by default).</param>
    public static IServiceCollection AddMigrationWorkflow(
        this IServiceCollection services,
        DatabaseProvider provider,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);

        return provider switch
        {
            DatabaseProvider.SqlServer => services.RegisterWorkflow<SqlServerMigrationWorkflow>(lifetime),
            DatabaseProvider.PostgreSql => services.RegisterWorkflow<PostgresMigrationWorkflow>(lifetime),
            DatabaseProvider.Oracle => services.RegisterWorkflow<OracleMigrationWorkflow>(lifetime),
            _ => throw new NotSupportedException($"Provider '{provider}' is not supported.")
        };
    }

    /// <summary>
    /// Registers the provider-specific workflow using the provider embedded in <see cref="DatabaseOptions"/>.
    /// </summary>
    public static IServiceCollection AddMigrationWorkflow(
        this IServiceCollection services,
        DatabaseOptions options,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(options);
        return services.AddMigrationWorkflow(options.Provider, lifetime);
    }

    private static IServiceCollection RegisterWorkflow<TWorkflow>(
        this IServiceCollection services,
        ServiceLifetime lifetime)
        where TWorkflow : class, IMigrationWorkflow
    {
        return lifetime switch
        {
            ServiceLifetime.Singleton => services.AddSingleton<IMigrationWorkflow, TWorkflow>(),
            ServiceLifetime.Transient => services.AddTransient<IMigrationWorkflow, TWorkflow>(),
            _ => services.AddScoped<IMigrationWorkflow, TWorkflow>()
        };
    }
}
