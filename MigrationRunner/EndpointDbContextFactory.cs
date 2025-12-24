using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Database.ECM.DbContexts;
using DataAccessLayer.Database.ECM.Interfaces;
using DataAccessLayer.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.Configuration;

namespace MigrationRunner;

/// <summary>
/// Builds provider-specific ECM DbContexts for a migration endpoint based on JSON options.
/// </summary>
public sealed class EndpointDbContextFactory : ISourceDbContextFactory, IDestinationDbContextFactory
{
    private readonly DatabaseOptions _options;
    private readonly Action<DbContextOptionsBuilder>? _configure;

    public EndpointDbContextFactory(DatabaseOptions options, Action<DbContextOptionsBuilder>? configure = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _configure = configure;
    }

    public Task<EcmDbContextBase> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_options.Provider switch
        {
            DatabaseProvider.SqlServer => BuildSqlServer(),
            DatabaseProvider.PostgreSql => BuildPostgres(),
            DatabaseProvider.Oracle => BuildOracle(),
            _ => throw new ProviderNotSupportedException($"Provider '{_options.Provider}' is not supported.")
        });
    }

    public EcmDbContextBase CreateDbContext()
    {
        return _options.Provider switch
        {
            DatabaseProvider.SqlServer => BuildSqlServer(),
            DatabaseProvider.PostgreSql => BuildPostgres(),
            DatabaseProvider.Oracle => BuildOracle(),
            _ => throw new ProviderNotSupportedException($"Provider '{_options.Provider}' is not supported.")
        };
    }

    private EcmDbContextBase BuildSqlServer()
    {
        var builder = new DbContextOptionsBuilder<EcmSqlServerDbContext>();
        ConfigureCommon(builder);
        var connectionString = ConnectionStringFactory.Build(_options);
        builder.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(EcmSqlServerDbContext).Assembly.FullName);
            if (_options.CommandTimeoutSeconds is { } timeout)
            {
                sql.CommandTimeout(timeout);
            }
        });
        return new EcmSqlServerDbContext(builder.Options);
    }

    private EcmDbContextBase BuildPostgres()
    {
        var builder = new DbContextOptionsBuilder<EcmPostgresDbContext>();
        ConfigureCommon(builder);
        var connectionString = ConnectionStringFactory.Build(_options);
        builder.UseNpgsql(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(EcmPostgresDbContext).Assembly.FullName);
            if (_options.CommandTimeoutSeconds is { } timeout)
            {
                sql.CommandTimeout(timeout);
            }
        });
        return new EcmPostgresDbContext(builder.Options);
    }

    private EcmDbContextBase BuildOracle()
    {
        var builder = new DbContextOptionsBuilder<EcmOracleDbContext>();
        ConfigureCommon(builder);
        var connectionString = ConnectionStringFactory.Build(_options);
        builder.UseOracle(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(EcmOracleDbContext).Assembly.FullName);
            if (_options.CommandTimeoutSeconds is { } timeout)
            {
                sql.CommandTimeout(timeout);
            }
        });
        return new EcmOracleDbContext(builder.Options);
    }

    private void ConfigureCommon(DbContextOptionsBuilder builder)
    {
        builder.EnableDetailedErrors(_options.EnableDetailedErrors);
        if (_options.EnableSensitiveDataLogging)
        {
            builder.EnableSensitiveDataLogging();
        }

        _configure?.Invoke(builder);
    }
}

public interface ISourceDbContextFactory : IEcmDbContextFactory;

public interface IDestinationDbContextFactory : IEcmDbContextFactory;
