using System.ComponentModel.DataAnnotations;

namespace Shared.Configuration;

/// <summary>
/// Describes a single "active" data source with provider-specific connection settings.
/// </summary>
public sealed class ActiveDataSourceOptions
{
    [Required]
    public DatabaseProvider Provider { get; set; }

    /// <summary>
    /// Optional fully formed connection string. When omitted the provider profile is used.
    /// </summary>
    public string? ConnectionString { get; set; }

    public SqlServerConnectionProfileOptions SqlServer { get; init; } = new();
    public PostgresConnectionProfileOptions Postgres { get; init; } = new();
    public OracleConnectionProfileOptions Oracle { get; init; } = new();

    public bool EnableDetailedErrors { get; init; } = true;
    public bool EnableSensitiveDataLogging { get; init; }

    public int? CommandTimeoutSeconds { get; init; }
    public int? ConnectionTimeoutSeconds { get; init; }

    public bool WrapProviderExceptions { get; init; } = true;

    public CommandPoolOptions CommandPool { get; init; } = new();
    public ResilienceOptions Resilience { get; init; } = new();
    public DiagnosticsOptions Diagnostics { get; init; } = new();
    public ParameterBindingOptions ParameterBinding { get; init; } = new();
    public InputNormalizationOptions InputNormalization { get; init; } = new();

    /// <summary>
    /// Constructs a <see cref="DatabaseOptions"/> instance using the active provider details.
    /// </summary>
    public DatabaseOptions ToDatabaseOptions()
    {
        var connectionString = string.IsNullOrWhiteSpace(ConnectionString)
            ? BuildConnectionString()
            : ConnectionString;

        return new DatabaseOptions
        {
            Provider = Provider,
            ConnectionString = connectionString,
            EnableDetailedErrors = EnableDetailedErrors,
            EnableSensitiveDataLogging = EnableSensitiveDataLogging,
            CommandTimeoutSeconds = CommandTimeoutSeconds,
            ConnectionTimeoutSeconds = ConnectionTimeoutSeconds,
            WrapProviderExceptions = WrapProviderExceptions,
            CommandPool = CommandPool,
            Resilience = Resilience,
            Diagnostics = Diagnostics,
            ParameterBinding = ParameterBinding,
            InputNormalization = InputNormalization
        };
    }

    private string BuildConnectionString()
    {
        return Provider switch
        {
            DatabaseProvider.SqlServer => SqlServer.BuildConnectionString(),
            DatabaseProvider.PostgreSql => Postgres.BuildConnectionString(),
            DatabaseProvider.Oracle => Oracle.BuildConnectionString(),
            _ => throw new InvalidOperationException($"Provider '{Provider}' is not supported.")
        };
    }
}
