using Shared.Configuration;

namespace MigrationRunner.Configuration;

/// <summary>
/// Root configuration for the migration runner host.
/// </summary>
public sealed class MigrationRunnerOptions
{
    public const string SectionName = "MigrationRunner";

    public DatabaseEndpointOptions Source { get; set; } = new();
    public DatabaseEndpointOptions Destination { get; set; } = new();
}

/// <summary>
/// Endpoint-specific options (provider, connection string, diagnostics).
/// </summary>
public sealed class DatabaseEndpointOptions
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;
    public int? Port { get; set; }
    public int? CommandTimeoutSeconds { get; set; }
    public int? ConnectionTimeoutSeconds { get; set; }
    public SqlServerConnectionProfileOptions SqlServer { get; set; } = new();
    public OracleConnectionProfileOptions Oracle { get; set; } = new();
    public PostgresConnectionProfileOptions Postgres { get; set; } = new();
}
