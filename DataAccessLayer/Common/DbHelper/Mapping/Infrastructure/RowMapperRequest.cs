using System.Collections.Generic;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Describes per-call overrides used when materializing result sets.
/// </summary>
/// <remarks>
/// Passed through to <see cref="IRowMapperFactory"/> by the top-level query helpers whenever callers need to alter strategy,
/// casing, or column overrides for a specific invocation.
/// </remarks>
public sealed class RowMapperRequest
{
    /// <summary>
    /// Gets the default request instance.
    /// </summary>
    public static RowMapperRequest Default { get; } = new();

    /// <summary>
    /// Gets or sets the preferred mapper strategy. When null the global default is used.
    /// </summary>
    public MapperStrategy? Strategy { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether column/property matching ignores casing. Null falls back to defaults.
    /// </summary>
    public bool? IgnoreCase { get; init; }

    /// <summary>
    /// Gets or sets an optional property-to-column mapping (PropertyName -> ColumnName).
    /// </summary>
    public IReadOnlyDictionary<string, string>? PropertyToColumnMap { get; init; }
}
