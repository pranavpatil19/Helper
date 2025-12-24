using System;
using System.Collections.Generic;
using System.Data;

namespace DataAccessLayer.Execution;

/// <summary>
/// Represents a normalized description of a database parameter that can be translated
/// into provider-specific implementations inside the DAL while preserving the explicit
/// <see cref="DbType"/> and other metadata configured by callers.
/// </summary>
/// <remarks>
/// Instances are typically produced by <see cref="DbParameterCollectionBuilder"/> or
/// <see cref="StructuredParameterBuilder"/> and later materialized into provider parameters
/// by <see cref="IParameterBinder"/>. Keeping the <see cref="DbType"/> explicit avoids inference
/// surprises across SQL Server, PostgreSQL, and Oracle.
/// </summary>
public sealed class DbParameterDefinition
{
    /// <summary>
    /// Gets or sets the logical parameter name without any provider-specific prefix (the helper adds it).
    /// Use descriptive names that match the <see cref="DbType"/> semantics so stored procedures stay readable.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the value to assign to the parameter before conversion.
    /// When <see cref="DbType"/> is set, the binder converts <see cref="Value"/> accordingly; otherwise provider inference is used.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Gets or sets the explicit <see cref="DbType"/> when automatic inference is not desired.
    /// Prefer setting this property on every definition (inputs, outputs, return values) so cross-provider behavior is deterministic.
    /// </summary>
    public DbType? DbType { get; init; }

    /// <summary>
    /// Gets or sets the direction for the parameter (input, output, return, etc.).
    /// The direction dictates when <see cref="Value"/> or <see cref="DbParameterDefinition.Values"/> are read/written by the provider.
    /// </summary>
    public ParameterDirection Direction { get; init; } = ParameterDirection.Input;

    /// <summary>
    /// Gets or sets the optional size for string/binary parameters.
    /// Always specify size when using <see cref="DbType.String"/> or <see cref="DbType.Binary"/> outputs to prevent provider truncation.
    /// </summary>
    public int? Size { get; init; }

    /// <summary>
    /// Gets or sets the precision for numeric parameters when <see cref="DbType"/> is decimal-based.
    /// </summary>
    public byte? Precision { get; init; }

    /// <summary>
    /// Gets or sets the scale for numeric parameters and informs normalization/rounding rules for <see cref="DbType.Decimal"/>.
    /// </summary>
    public byte? Scale { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the parameter accepts null values.
    /// Combine with <see cref="DbType"/> to make intent clear for nullable database columns.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Gets or sets the fallback value to apply when <see cref="Value"/> is null.
    /// This is useful for defaulting null input parameters to sentinel values that match the chosen <see cref="DbType"/>.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Gets or sets the provider-specific data type name when required (e.g., PostgreSQL arrays).
    /// Pair this with <see cref="DbType"/> (for example, <c>DbType.Guid</c> plus <c>"_uuid"</c>) so both the provider and DAL agree on the type.
    /// </summary>
    public string? ProviderTypeName { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the parameter should be treated as a list
    /// (e.g., IN clause expansion or provider-specific array binding).
    /// When <c>true</c>, set <see cref="DbType"/> to the element type and populate <see cref="Values"/>.
    /// </summary>
    public bool TreatAsList { get; init; }

    /// <summary>
    /// Gets or sets the explicit list of values to use when <see cref="TreatAsList"/> is <c>true</c>.
    /// Ensure every value matches the configured <see cref="DbType"/> to avoid provider conversion errors.
    /// </summary>
    public IReadOnlyList<object?>? Values { get; init; }

    /// <summary>
    /// Gets or sets the optional converter applied before pushing the value to the provider.
    /// Converters can adjust formatting or coercion while still respecting the declared <see cref="DbType"/>.
    /// </summary>
    public Func<object?, object?>? ValueConverter { get; init; }
}
