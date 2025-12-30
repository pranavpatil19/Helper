using System;
using DataAccessLayer.DependencyInjection.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;

namespace DataAccessLayer;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Wires up ECM-specific repositories/contexts using an active provider profile.
    /// </summary>
    public static IServiceCollection AddEcmEntityFrameworkSupport(
        this IServiceCollection services,
        ActiveDataSourceOptions activeDataSource,
        Action<DbContextOptionsBuilder>? configure = null,
        bool registerDefaultServices = true)
    {
        ArgumentNullException.ThrowIfNull(activeDataSource);
        return services.AddEcmEntityFrameworkSupport(
            activeDataSource.ToDatabaseOptions(),
            configure,
            registerDefaultServices);
    }

    /// <summary>
    /// Wires up ECM-specific repositories and DbContexts for the configured provider.
    /// </summary>
    /// <param name="registerDefaultServices">Adds the built-in EF repository + migration services when true (default).</param>
    public static IServiceCollection AddEcmEntityFrameworkSupport(
        this IServiceCollection services,
        DatabaseOptions options,
        Action<DbContextOptionsBuilder>? configure = null,
        bool registerDefaultServices = true)
    {
        DalEfServiceRegistrar.Register(services, options, configure, registerDefaultServices);
        return services;
    }
}
