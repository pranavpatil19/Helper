using System;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Database.ECM.DbContexts;
using DataAccessLayer.EF;
using DataAccessLayer.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Configuration;

namespace DataAccessLayer.DependencyInjection.Common;

internal static class DalEfServiceRegistrar
{
    public static void Register(
        IServiceCollection services,
        DatabaseOptions options,
        Action<DbContextOptionsBuilder>? configure,
        bool registerDefaultServices)
    {
        _ = services ?? throw new ArgumentNullException(nameof(services));
        _ = options ?? throw new ArgumentNullException(nameof(options));

        if (registerDefaultServices)
        {
            services.TryAddScoped<IMigrationService, MigrationService>();
        }

        RegisterEcmDbContextFactory(services, options, configure);
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
}
