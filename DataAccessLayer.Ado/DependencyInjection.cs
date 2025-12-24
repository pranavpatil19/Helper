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
}
