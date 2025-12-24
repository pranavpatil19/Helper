using System;
using DataAccessLayer.Exceptions;
using Shared.Configuration;

namespace MigrationRunner.Configuration;

internal static class DatabaseEndpointOptionsExtensions
{
    public static DatabaseOptions ToDatabaseOptions(this DatabaseEndpointOptions endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var connectionString = ResolveConnectionString(endpoint);
        return new DatabaseOptions
        {
            Provider = endpoint.Provider,
            ConnectionString = connectionString,
            ConnectionStringPort = endpoint.Port,
            EnableDetailedErrors = endpoint.EnableDetailedErrors,
            EnableSensitiveDataLogging = endpoint.EnableSensitiveDataLogging,
            WrapProviderExceptions = endpoint.WrapProviderExceptions,
            CommandTimeoutSeconds = endpoint.CommandTimeoutSeconds,
            ConnectionTimeoutSeconds = endpoint.ConnectionTimeoutSeconds
        };
    }

    private static string ResolveConnectionString(DatabaseEndpointOptions endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint.ConnectionString))
        {
            return endpoint.ConnectionString;
        }

        try
        {
            return endpoint.Provider switch
            {
                DatabaseProvider.SqlServer => endpoint.SqlServer.BuildConnectionString(),
                DatabaseProvider.PostgreSql => endpoint.Postgres.BuildConnectionString(),
                DatabaseProvider.Oracle => endpoint.Oracle.BuildConnectionString(),
                _ => throw new DalConfigurationException($"Provider '{endpoint.Provider}' is not supported.")
            };
        }
        catch (InvalidOperationException ex)
        {
            throw new DalConfigurationException($"Invalid {endpoint.Provider} connection profile configuration.", ex);
        }
    }
}
