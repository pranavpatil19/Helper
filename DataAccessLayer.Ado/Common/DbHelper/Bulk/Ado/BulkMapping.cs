using System;
using System.Collections.Generic;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Base class describing a table and its columns for bulk operations.
/// </summary>
/// <typeparam name="T">Row/entity type.</typeparam>
public abstract class BulkMapping<T> : IRowProjection<T>
{
    protected BulkMapping(
        string tableName,
        IReadOnlyList<BulkColumn> columns,
        Func<T, object?[]> valueSelector)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        TableName = tableName;
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        if (Columns.Count == 0)
        {
            throw new ArgumentException("At least one column must be defined.", nameof(columns));
        }

        ValueSelector = valueSelector ?? throw new ArgumentNullException(nameof(valueSelector));
    }

    /// <summary>
    /// Gets the destination table name.
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// Gets the ordered column definitions.
    /// </summary>
    public IReadOnlyList<BulkColumn> Columns { get; }

    /// <summary>
    /// Gets the delegate that projects a row into column values.
    /// </summary>
    public Func<T, object?[]> ValueSelector { get; }

    /// <inheritdoc />
    public object?[] Project(T value) => ValueSelector(value);
}
