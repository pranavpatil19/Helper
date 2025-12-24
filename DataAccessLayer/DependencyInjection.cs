using System.Linq;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using DataAccessLayer.Transactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;
using DataAccessLayer.Execution;
using Microsoft.Extensions.Logging;
using FluentValidation;
using DataAccessLayer.Validation;
using DataAccessLayer.Telemetry;
using DataAccessLayer.Configuration;
using DataAccessLayer.Mapping;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Exceptions;
using DataAccessLayer.Database.ECM.Interfaces;
using DataAccessLayer.Database.ECM.Services;
using DataAccessLayer.Database.ECM.DbContexts;
using DataAccessLayer.EF;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccessLayer(
        this IServiceCollection services,
        DatabaseOptions options,
        Action<DbHelperOptions>? configureHelper = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        var resolvedFeatures = DalFeatureDefaults.Resolve(options);
        services.AddSingleton(resolvedFeatures);

        // 1. Options -------------------------------------------------
        _ = RegisterHelperOptions(services, configureHelper);             // DbHelperOptions: mapper caching, default strategy, etc.

        // 2. Cross-cutting plumbing (telemetry, mapping, infrastructure, validation, command factory).
        services.AddDalTelemetry(resolvedFeatures);                      // IDataAccessTelemetry (real or no-op).
        RegisterMappingInfrastructure(services, options);                 // RowMapperFactory + provider-specific profiles.
        RegisterCoreInfrastructure(services, resolvedFeatures);           // Connection factory, scope manager, resilience, binder.
        services.AddDalValidation();                                      // FluentValidation pipeline.
        RegisterCommandFactory(services);                                 // DbCommandFactory pooling/parameter binding.

        // 3. Transactions and the high-level DatabaseHelper.
        services.AddDalTransactions(resolvedFeatures);
        services.AddScoped<IDatabaseHelper, DatabaseHelper>();

        // 4. Bulk engines + orchestration helper (guarded by EnabledBulkProviders).
        services.AddDalBulkEngines(resolvedFeatures);

        // 5. Domain-specific services (repository, EF DbContext factory) if EF helpers are enabled.
        // EF support is opt-in via AddEcmEntityFrameworkSupport; no registration here.

        return services;
    }

    public static IServiceCollection AddDataAccessLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbHelperOptions>? configureHelper = null)
    {
        var options = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>()
            ?? throw new DalConfigurationException($"Missing '{DatabaseOptions.SectionName}' configuration section.");

        return services.AddDataAccessLayer(options, configureHelper);
    }

    /// <summary>
    /// Registers DbHelperOptions so downstream services can read mapper/command defaults.
    /// </summary>
    private static DbHelperOptions RegisterHelperOptions(IServiceCollection services, Action<DbHelperOptions>? configureHelper)
    {
        var helperOptions = new DbHelperOptions();
        configureHelper?.Invoke(helperOptions);
        services.AddSingleton(helperOptions);
        return helperOptions;
    }

    /// <summary>
    /// Registers mapper profiles/normalizers and the RowMapperFactory.
    /// </summary>
    private static void RegisterMappingInfrastructure(IServiceCollection services, DatabaseOptions options)
    {
        services.AddSingleton<IMappingProfile, EnumMappingProfile>();

        if (options.Provider == DatabaseProvider.Oracle)
        {
            services.AddSingleton<IMappingProfile, OracleBooleanMappingProfile>();
            services.AddSingleton<IMappingProfile, OracleDateTimeMappingProfile>();
        }
        else if (options.Provider == DatabaseProvider.PostgreSql)
        {
            services.AddSingleton<IColumnNameNormalizer, PostgresSnakeCaseColumnNameNormalizer>();
        }

        services.AddSingleton<IRowMapperFactory>(sp =>
        {
            var helperOptionsResolved = sp.GetRequiredService<DbHelperOptions>();
            var profiles = sp.GetServices<IMappingProfile>();
            var normalizer = sp.GetService<IColumnNameNormalizer>();
            return new RowMapperFactory(helperOptionsResolved, profiles, normalizer);
        });
    }

    /// <summary>
    /// Registers connection factory, scope manager, resilience strategy (optional), and parameter binder.
    /// </summary>
    private static void RegisterCoreInfrastructure(IServiceCollection services, DalFeatures features)
    {
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddSingleton<IDbParameterPool>(sp =>
            new DbParameterPool(sp.GetRequiredService<DatabaseOptions>().CommandPool));
        services.AddSingleton<IConnectionScopeManager>(sp =>
            new ConnectionScopeManager(
                sp.GetRequiredService<IDbConnectionFactory>(),
                sp.GetRequiredService<DatabaseOptions>()));
        services.AddSingleton<IResilienceStrategy>(sp =>
        {
            if (!features.Resilience)
            {
                return NoOpResilienceStrategy.Instance;
            }

            return new ResilienceStrategy(
                sp.GetRequiredService<DatabaseOptions>().Resilience,
                sp.GetRequiredService<ILogger<ResilienceStrategy>>());
        });
        services.AddSingleton<IParameterBinder>(sp =>
        {
            var dbOptions = sp.GetRequiredService<DatabaseOptions>();
            return new ParameterBinder(
                sp.GetRequiredService<IDbParameterPool>(),
                dbOptions.ParameterBinding,
                dbOptions.InputNormalization);
        });
    }

    /// <summary>
    /// Registers the pooled DbCommandFactory that every helper uses.
    /// </summary>
    private static void RegisterCommandFactory(IServiceCollection services)
    {
        services.AddSingleton<IDbCommandFactory>(sp =>
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

    /// <summary>
    /// Optional helper to wire up ECM EF Core contexts + repositories when EF is desired.
    /// </summary>
    public static IServiceCollection AddEcmEntityFrameworkSupport(
        this IServiceCollection services,
        DatabaseOptions options,
        Action<DbContextOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

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

    /// <summary>
    /// Registers the provider-specific ECM DbContext + adapter factory.
    /// </summary>
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
}
