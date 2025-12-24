using CoreBusiness.Validation;
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
    public RunnerLoggingOptions Logging { get; set; } = new();
    public ValidationOptions Validation { get; set; } = new();
}

/// <summary>
/// Endpoint-specific options (provider, connection string, diagnostics).
/// </summary>
public sealed class DatabaseEndpointOptions
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;
    public string ConnectionString { get; set; } = string.Empty;
    public int? Port { get; set; }
    public bool EnableDetailedErrors { get; set; } = true;
    public bool EnableSensitiveDataLogging { get; set; }
    public bool WrapProviderExceptions { get; set; } = true;
    public int? CommandTimeoutSeconds { get; set; }
    public int? ConnectionTimeoutSeconds { get; set; }
    public SqlServerConnectionProfileOptions SqlServer { get; set; } = new();
    public OracleConnectionProfileOptions Oracle { get; set; } = new();
    public PostgresConnectionProfileOptions Postgres { get; set; } = new();
}

/// <summary>
/// Optional logging configuration (file sink, etc.).
/// </summary>
public sealed class RunnerLoggingOptions
{
    public string? LogPath { get; set; }
}
