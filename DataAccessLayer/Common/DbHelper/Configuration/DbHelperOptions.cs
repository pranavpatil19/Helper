using DataAccessLayer.Mapping;

namespace DataAccessLayer.Configuration;

/// <summary>
/// Provides DAL-specific tuning knobs that sit above <see cref="Shared.Configuration.DatabaseOptions"/>.
/// </summary>
public sealed class DbHelperOptions
{
    /// <summary>
    /// Gets or sets the default mapper strategy used when callers rely on automatic row mapping.
    /// </summary>
    public MapperStrategy DefaultMapperStrategy { get; set; } = MapperStrategy.Reflection;

    /// <summary>
    /// Gets or sets a value indicating whether column/property name comparison ignores casing.
    /// </summary>
    public bool IgnoreCase { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether mapper instances should be cached per type/strategy.
    /// </summary>
    public bool EnableMapperCaching { get; set; } = true;

    /// <summary>
    /// Gets telemetry/diagnostics settings that influence logging/tracing.
    /// </summary>
    public TelemetryOptions Telemetry { get; init; } = new();
}
