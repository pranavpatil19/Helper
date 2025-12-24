using System;
using System.Collections.Generic;
using System.Data;

namespace DataAccessLayer.Execution;

/// <summary>
/// Represents a normalized description of a database parameter that can be translated
/// into provider-specific implementations inside the DAL.
/// </summary>
public sealed class DbParameterDefinition
{
    /// <summary>
    /// Gets or sets the logical parameter name without any provider-specific prefix (the helper adds it).
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the value to assign to the parameter before conversion.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Gets or sets the explicit <see cref="DbType"/> when automatic inference is not desired.
    /// </summary>
    public DbType? DbType { get; init; }

    /// <summary>
    /// Gets or sets the direction for the parameter (input, output, return, etc.).
    /// </summary>
    public ParameterDirection Direction { get; init; } = ParameterDirection.Input;

    /// <summary>
    /// Gets or sets the optional size for string/binary parameters.
    /// </summary>
    public int? Size { get; init; }

    /// <summary>
    /// Gets or sets the precision for numeric parameters.
    /// </summary>
    public byte? Precision { get; init; }

    /// <summary>
    /// Gets or sets the scale for numeric parameters.
    /// </summary>
    public byte? Scale { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the parameter accepts null values.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Gets or sets the fallback value to apply when <see cref="Value"/> is null.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Gets or sets the provider-specific data type name when required (e.g., PostgreSQL arrays).
    /// </summary>
    public string? ProviderTypeName { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the parameter should be treated as a list
    /// (e.g., IN clause expansion or provider-specific array binding).
    /// </summary>
    public bool TreatAsList { get; init; }

    /// <summary>
    /// Gets or sets the explicit list of values to use when <see cref="TreatAsList"/> is <c>true</c>.
    /// </summary>
    public IReadOnlyList<object?>? Values { get; init; }

    /// <summary>
    /// Gets or sets the optional converter applied before pushing the value to the provider.
    /// </summary>
    public Func<object?, object?>? ValueConverter { get; init; }
}
