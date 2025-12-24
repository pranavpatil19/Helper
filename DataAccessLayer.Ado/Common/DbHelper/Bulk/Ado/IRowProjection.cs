using System.Collections.Generic;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Describes how an entity projects into tabular columns (used by bulk writers and mappers).
/// </summary>
/// <typeparam name="T">Entity type.</typeparam>
public interface IRowProjection<in T>
{
    /// <summary>
    /// Gets the destination table name, when applicable.
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// Gets the ordered columns that participate in the projection.
    /// </summary>
    IReadOnlyList<BulkColumn> Columns { get; }

    /// <summary>
    /// Projects the supplied entity into an ordered column array.
    /// </summary>
    object?[] Project(T value);
}
