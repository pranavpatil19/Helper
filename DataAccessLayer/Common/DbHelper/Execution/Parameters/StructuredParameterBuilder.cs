using System;
using System.Collections.Generic;
using System.Data;

namespace DataAccessLayer.Execution;

/// <summary>
/// Helper methods to create structured parameter definitions for provider-specific features.
/// Every helper ensures the resulting <see cref="DbParameterDefinition"/> carries a precise <see cref="DbType"/>
/// (or provider type name) so the database can bind the payload deterministically.
/// </summary>
public static class StructuredParameterBuilder
{
    /// <summary>
    /// Creates a SQL Server table-valued parameter (TVP) definition.
    /// </summary>
    /// <param name="name">Logical parameter name without the provider prefix (no '@').</param>
    /// <param name="value">Value assigned to the TVP (e.g., <see cref="DataTable"/> or <see cref="IEnumerable{T}"/> of records).</param>
    /// <param name="typeName">User-defined table type name registered in SQL Server.</param>
    /// <returns>A <see cref="DbParameterDefinition"/> configured with <see cref="DbType.Object"/> and the supplied type name.</returns>
    /// <remarks>
    /// Use this helper when SQL Server stored procedures or statements expect TVPs. SQL Client binds TVPs using
    /// the structured type name, so the builder stores it in <see cref="DbParameterDefinition.ProviderTypeName"/>.
    /// Remember to add other parameters with an explicit <see cref="DbType"/> using <see cref="DbParameterCollectionBuilder"/>.
    /// </remarks>
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
    /// <typeparam name="T">Element type contributed by <paramref name="values"/>.</typeparam>
    /// <param name="name">Logical parameter name.</param>
    /// <param name="values">Values to send as a PostgreSQL array.</param>
    /// <param name="dbType">Explicit <see cref="DbType"/> that matches the array element type (for example, <see cref="DbType.Guid"/>).</param>
    /// <param name="providerTypeName">Optional provider type name (for example, "_uuid" or "_int4").</param>
    /// <returns>A <see cref="DbParameterDefinition"/> flagged as a list parameter.</returns>
    /// <remarks>
    /// PostgreSQL distinguishes CLR type inference from provider type names. When binding Guids, pass <c>dbType: DbType.Guid</c>
    /// and <c>providerTypeName: "_uuid"</c> to ensure the driver treats the payload as an array of UUIDs.
    /// </remarks>
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
    /// Creates an Oracle array-binding parameter definition.
    /// </summary>
    /// <typeparam name="T">Element type contributed by <paramref name="values"/>.</typeparam>
    /// <param name="name">Logical Oracle parameter name.</param>
    /// <param name="values">Values destined for array binding.</param>
    /// <param name="dbType">Base <see cref="DbType"/> requested by the Oracle provider.</param>
    /// <param name="size">Optional size for character arrays (for example, <c>size: 64</c>).</param>
    /// <returns>A <see cref="DbParameterDefinition"/> prepared for Oracle array binding.</returns>
    /// <remarks>
    /// Oracle's managed provider requires additional metadata (captured as <c>ProviderTypeName = "ArrayBind"</c>).
    /// Specify the <see cref="DbType"/> explicitly so the driver can map the payload (e.g., <see cref="DbType.String"/> or <see cref="DbType.Decimal"/>).
    /// </remarks>
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
