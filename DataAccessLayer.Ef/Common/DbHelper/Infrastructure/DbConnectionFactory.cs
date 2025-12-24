using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using Shared.Configuration;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Creates provider-specific <see cref="DbConnection"/> instances with diagnostics applied.
/// </summary>
public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly ILogger<DbConnectionFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbConnectionFactory"/> class.
    /// </summary>
    /// <param name="logger">Structured logger used for timeout diagnostics.</param>
    public DbConnectionFactory(ILogger<DbConnectionFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a provider-specific <see cref="DbConnection"/> using the supplied options.
    /// </summary>
    /// <param name="options">Database options containing provider, connection string, and diagnostics.</param>
    /// <returns>An unopened <see cref="DbConnection"/> instance.</returns>
    /// <exception cref="ProviderNotSupportedException">Thrown when the provider is unknown.</exception>
    public DbConnection CreateConnection(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);

        var connectionString = ConnectionStringFactory.Build(options);
        DbConnection connection = options.Provider switch
        {
            DatabaseProvider.SqlServer => new SqlConnection(connectionString),
            DatabaseProvider.PostgreSql => new NpgsqlConnection(connectionString),
            DatabaseProvider.Oracle => new OracleConnection(connectionString),
            _ => throw new ProviderNotSupportedException($"Provider '{options.Provider}' is not supported.")
        };

        LogConnectionTimeout(options);
        return connection;
    }

    private void LogConnectionTimeout(DatabaseOptions options)
    {
        if (!options.Diagnostics.LogEffectiveTimeouts)
        {
            return;
        }

        if (options.ConnectionTimeoutSeconds is not { } timeout)
        {
            return;
        }

        _logger.LogInformation(
            "Applied connection timeout {TimeoutSeconds}s for provider {Provider}.",
            timeout,
            options.Provider);
    }
}
