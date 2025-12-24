using System.ComponentModel.DataAnnotations;

namespace Shared.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public DatabaseProvider Provider { get; init; }

    [Required]
    public string ConnectionString { get; init; } = string.Empty;
    public int? ConnectionStringPort { get; init; }

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
}
