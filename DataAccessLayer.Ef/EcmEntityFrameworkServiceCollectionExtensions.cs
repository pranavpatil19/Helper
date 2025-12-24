using System;
using System.Collections.Generic;
using System.Linq;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using DataAccessLayer.Configuration;
using DataAccessLayer.Database.ECM.DbContexts;
using DataAccessLayer.Database.ECM.Interfaces;
using DataAccessLayer.Database.ECM.Services;
using DataAccessLayer.Exceptions;
using DataAccessLayer.Execution;
using DataAccessLayer.Mapping;
using DataAccessLayer.Transactions;
using DataAccessLayer.Telemetry;
using DataAccessLayer.Validation;
using DataAccessLayer.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Shared.Configuration;

namespace DataAccessLayer;

/// <summary>
/// Adds Entity Framework Core helpers (DbContexts, repositories, migration services) on top of the ADO.NET DAL.
/// </summary>
public static class EcmEntityFrameworkServiceCollectionExtensions
{
    /// <summary>
    /// Registers provider-specific ECM DbContexts, repositories, and the migration service.
    /// No-op when EF helpers are disabled via <see cref="DalFeatures"/>.
    /// </summary>
    public static IServiceCollection AddEcmEntityFrameworkSupport(
        this IServiceCollection services,
        DatabaseOptions options,
        Action<DbContextOptionsBuilder>? configure = null,
        Action<DbHelperOptions>? configureHelper = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        var resolvedFeatures = EnsureOptions(services, options);
        _ = RegisterHelperOptions(services, configureHelper);

        services.AddDalTelemetry(resolvedFeatures);
        RegisterCoreInfrastructure(services, resolvedFeatures);
        services.AddDalValidation();
        RegisterCommandFactory(services);

        if (!EfHelpersEnabled(services))
        {
            return services;
        }

        services.AddScoped<ITodoRepository, TodoRepository>();
        services.AddScoped<IMigrationService, MigrationService>();
        RegisterEcmDbContextFactory(services, options, configure);
        return services;
    }

    private static bool EfHelpersEnabled(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(DalFeatures));
        if (descriptor?.ImplementationInstance is DalFeatures features)
        {
            return features.EfHelpers;
        }

        return true;
    }

    private static void RegisterEcmDbContextFactory(
        IServiceCollection services,
        DatabaseOptions options,
        Action<DbContextOptionsBuilder>? configure)
    {
        switch (options.Provider)
        {
            case DatabaseProvider.SqlServer:
                services.AddDbContextFactory<EcmSqlServerDbContext>((sp, builder) =>
                {
                    ConfigureDbContext(builder, options, typeof(EcmSqlServerDbContext));
                    configure?.Invoke(builder);
                });
                services.AddScoped<IEcmDbContextFactory>(sp =>
                    new EcmDbContextFactoryAdapter<EcmSqlServerDbContext>(
                        sp.GetRequiredService<IDbContextFactory<EcmSqlServerDbContext>>()));
                break;

            case DatabaseProvider.PostgreSql:
                services.AddDbContextFactory<EcmPostgresDbContext>((sp, builder) =>
                {
                    ConfigureDbContext(builder, options, typeof(EcmPostgresDbContext));
                    configure?.Invoke(builder);
                });
                services.AddScoped<IEcmDbContextFactory>(sp =>
                    new EcmDbContextFactoryAdapter<EcmPostgresDbContext>(
                        sp.GetRequiredService<IDbContextFactory<EcmPostgresDbContext>>()));
                break;

            case DatabaseProvider.Oracle:
                services.AddDbContextFactory<EcmOracleDbContext>((sp, builder) =>
                {
                    ConfigureDbContext(builder, options, typeof(EcmOracleDbContext));
                    configure?.Invoke(builder);
                });
                services.AddScoped<IEcmDbContextFactory>(sp =>
                    new EcmDbContextFactoryAdapter<EcmOracleDbContext>(
                        sp.GetRequiredService<IDbContextFactory<EcmOracleDbContext>>()));
                break;

            default:
                throw new ProviderNotSupportedException($"Provider '{options.Provider}' is not supported for ECM DbContexts.");
        }
    }

    private static void ConfigureDbContext(
        DbContextOptionsBuilder builder,
        DatabaseOptions options,
        Type migrationsAssemblySource)
    {
        var migrationsAssembly = migrationsAssemblySource.Assembly.FullName;
        builder.EnableDetailedErrors(options.EnableDetailedErrors);
        if (options.EnableSensitiveDataLogging)
        {
            builder.EnableSensitiveDataLogging();
        }

        var connectionString = ConnectionStringFactory.Build(options);
        var commandTimeout = options.CommandTimeoutSeconds;

        _ = options.Provider switch
        {
            DatabaseProvider.SqlServer => builder.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(migrationsAssembly);
                if (commandTimeout is { } timeout)
                {
                    sql.CommandTimeout(timeout);
                }
            }),
            DatabaseProvider.PostgreSql => builder.UseNpgsql(connectionString, sql =>
            {
                sql.MigrationsAssembly(migrationsAssembly);
                if (commandTimeout is { } timeout)
                {
                    sql.CommandTimeout(timeout);
                }
            }),
            DatabaseProvider.Oracle => builder.UseOracle(connectionString, sql =>
            {
                sql.MigrationsAssembly(migrationsAssembly);
                if (commandTimeout is { } timeout)
                {
                    sql.CommandTimeout(timeout);
                }
            }),
            _ => throw new ProviderNotSupportedException($"Provider '{options.Provider}' is not supported.")
        };
    }

    private static DalFeatures EnsureOptions(IServiceCollection services, DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        if (!services.Any(sd => sd.ServiceType == typeof(DatabaseOptions)))
        {
            services.AddSingleton(options);
        }

        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(DalFeatures));
        if (descriptor?.ImplementationInstance is DalFeatures existing)
        {
            return existing;
        }

        var resolved = DalFeatureDefaults.Resolve(options);
        services.AddSingleton(resolved);
        return resolved;
    }

    private static DbHelperOptions RegisterHelperOptions(IServiceCollection services, Action<DbHelperOptions>? configureHelper)
    {
        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(DbHelperOptions));
        if (descriptor?.ImplementationInstance is DbHelperOptions existing)
        {
            configureHelper?.Invoke(existing);
            return existing;
        }

        var helperOptions = new DbHelperOptions();
        configureHelper?.Invoke(helperOptions);
        services.AddSingleton(helperOptions);
        return helperOptions;
    }

    private static void RegisterCoreInfrastructure(IServiceCollection services, DalFeatures features)
    {
        services.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.TryAddSingleton<IDbParameterPool>(sp =>
            new DbParameterPool(sp.GetRequiredService<DatabaseOptions>().CommandPool));
        services.TryAddSingleton<IConnectionScopeManager>(sp =>
            new ConnectionScopeManager(
                sp.GetRequiredService<IDbConnectionFactory>(),
                sp.GetRequiredService<DatabaseOptions>()));
        services.TryAddSingleton<IResilienceStrategy>(sp =>
        {
            if (!features.Resilience)
            {
                return NoOpResilienceStrategy.Instance;
            }

            return new ResilienceStrategy(
                sp.GetRequiredService<DatabaseOptions>().Resilience,
                sp.GetRequiredService<ILogger<ResilienceStrategy>>());
        });
        services.TryAddSingleton<IParameterBinder>(sp =>
        {
            var dbOptions = sp.GetRequiredService<DatabaseOptions>();
            return new ParameterBinder(
                sp.GetRequiredService<IDbParameterPool>(),
                dbOptions.ParameterBinding,
                dbOptions.InputNormalization);
        });
    }

    private static void RegisterCommandFactory(IServiceCollection services)
    {
        services.TryAddSingleton<IDbCommandFactory>(sp =>
        {
            var dbOptions = sp.GetRequiredService<DatabaseOptions>();
            return new DbCommandFactory(
                sp.GetRequiredService<IParameterBinder>(),
                sp.GetRequiredService<IDbParameterPool>(),
                dbOptions,
                dbOptions.CommandPool,
                sp.GetRequiredService<ILogger<DbCommandFactory>>());
        });
    }
}
