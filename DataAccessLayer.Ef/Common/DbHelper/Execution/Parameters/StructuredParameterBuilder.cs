using System;
using System.Collections.Generic;
using System.Data;

namespace DataAccessLayer.Execution;

/// <summary>
/// Helper methods to create structured parameter definitions for provider-specific features.
/// </summary>
public static class StructuredParameterBuilder
{
    /// <summary>
    /// Creates a Table-Valued Parameter definition for SQL Server.
    /// </summary>
    /// <param name="name">Logical parameter name without '@'.</param>
    /// <param name="value">Value assigned to the TVP (DataTable or IEnumerable of records).</param>
    /// <param name="typeName">User-defined table type name.</param>
    public static DbParameterDefinition SqlServerTableValuedParameter(
        string name,
        object value,
        string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        return new DbParameterDefinition
        {
            Name = name,
            Direction = ParameterDirection.Input,
            Value = value,
            ProviderTypeName = typeName,
            DbType = DbType.Object
        };
    }

    /// <summary>
    /// Creates a PostgreSQL array parameter definition.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="name">Parameter name.</param>
    /// <param name="values">Values to send as array.</param>
    /// <param name="dbType">Optional DbType override.</param>
    /// <param name="providerTypeName">Optional explicit provider type name (e.g., "_uuid").</param>
    public static DbParameterDefinition PostgresArray<T>(
        string name,
        IEnumerable<T> values,
        DbType? dbType = null,
        string? providerTypeName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new DbParameterDefinition
        {
            Name = name,
            Direction = ParameterDirection.Input,
            TreatAsList = true,
            Values = values is IReadOnlyList<object?> list
                ? list
                : Materialize(values),
            DbType = dbType,
            ProviderTypeName = providerTypeName
        };
    }

    /// <summary>
    /// Creates an Oracle array-binding parameter.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="name">Parameter name.</param>
    /// <param name="values">Values to bind.</param>
    /// <param name="dbType">DbType representing the Oracle base type.</param>
    /// <param name="size">Optional size for varchar-based arrays.</param>
    public static DbParameterDefinition OracleArray<T>(
        string name,
        IEnumerable<T> values,
        DbType dbType,
        int? size = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new DbParameterDefinition
        {
            Name = name,
            Direction = ParameterDirection.Input,
            TreatAsList = true,
            Values = values is IReadOnlyList<object?> list
                ? list
                : Materialize(values),
            DbType = dbType,
            Size = size,
            ProviderTypeName = "ArrayBind"
        };
    }

    private static IReadOnlyList<object?> Materialize<T>(IEnumerable<T> values)
    {
        var result = new List<object?>();
        foreach (var value in values)
        {
            result.Add(value);
        }

        return result;
    }
}
