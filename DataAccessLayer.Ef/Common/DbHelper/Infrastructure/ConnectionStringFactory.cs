using Microsoft.Data.SqlClient;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Helper for producing provider-specific connection strings with global options applied.
/// </summary>
public static class ConnectionStringFactory
{
    public static string Build(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);

        if (options.ConnectionTimeoutSeconds is not { } timeout)
        {
            return options.ConnectionString;
        }

        return options.Provider switch
        {
            DatabaseProvider.SqlServer => BuildSqlServer(options.ConnectionString, timeout),
            DatabaseProvider.PostgreSql => BuildPostgres(options.ConnectionString, timeout),
            DatabaseProvider.Oracle => BuildOracle(options.ConnectionString, timeout),
            _ => options.ConnectionString
        };
    }

    private static string BuildSqlServer(string connectionString, int timeout)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ConnectTimeout = timeout
        };

        return builder.ConnectionString;
    }

    private static string BuildPostgres(string connectionString, int timeout)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = timeout
        };

        return builder.ConnectionString;
    }

    private static string BuildOracle(string connectionString, int timeout)
    {
        var builder = new OracleConnectionStringBuilder(connectionString)
        {
            ConnectionTimeout = timeout
        };

        return builder.ConnectionString;
    }
}
