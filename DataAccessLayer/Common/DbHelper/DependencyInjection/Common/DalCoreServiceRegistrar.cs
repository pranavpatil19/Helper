using System;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using DataAccessLayer.Configuration;
using DataAccessLayer.Execution;
using DataAccessLayer.Mapping;
using DataAccessLayer.Telemetry;
using DataAccessLayer.Transactions;
using DataAccessLayer.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Configuration;

namespace DataAccessLayer.DependencyInjection.Common;

internal static class DalCoreServiceRegistrar
{
    public static void Register(
        IServiceCollection services,
        DatabaseOptions options,
        Action<DbHelperOptions>? configureHelper,
        Action<DalServiceRegistrationOptions>? configureServices)
    {
        _ = services ?? throw new ArgumentNullException(nameof(services));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        services.AddSingleton(options);

        _ = RegisterHelperOptions(services, configureHelper);
        var registration = DalServiceRegistrationOptions.Create(configureServices);
        services.AddSingleton(new DalRuntimeOptions
        {
            EnableDetailedLogging = registration.EnableDetailedLogging
        });

        services.AddDalTelemetry(registration.EnableTelemetry);
        RegisterMappingInfrastructure(services, options);
        RegisterCoreInfrastructure(services, registration);
        services.AddDalValidation();
        RegisterCommandFactory(services);

        services.AddDalTransactions();
        services.AddScoped<IDatabaseHelper, DatabaseHelper>();

        services.AddDalBulkEngines();
    }

    private static DbHelperOptions RegisterHelperOptions(
        IServiceCollection services,
        Action<DbHelperOptions>? configureHelper)
    {
        var helperOptions = new DbHelperOptions();
        configureHelper?.Invoke(helperOptions);
        services.AddSingleton(helperOptions);
        return helperOptions;
    }

    private static void RegisterMappingInfrastructure(
        IServiceCollection services,
        DatabaseOptions options)
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

    private static void RegisterCoreInfrastructure(
        IServiceCollection services,
        DalServiceRegistrationOptions registration)
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
            if (!registration.EnableResilience)
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
}
