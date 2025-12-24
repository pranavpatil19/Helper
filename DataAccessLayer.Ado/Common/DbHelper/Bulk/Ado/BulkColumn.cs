using System;
using System.Data;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Describes how a source member maps to a destination column.
/// </summary>
public sealed class BulkColumn
{
    public BulkColumn(
        string columnName,
        DbType? dbType = null,
        bool isKey = false,
        bool isIdentity = false,
        bool isNullable = true,
        string? providerTypeName = null)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ArgumentException("Column name is required.", nameof(columnName));
        }

        ColumnName = columnName;
        DbType = dbType;
        IsKey = isKey;
        IsIdentity = isIdentity;
        IsNullable = !isIdentity && isNullable;
        ProviderTypeName = providerTypeName;
    }

    /// <summary>
    /// Destination column name.
    /// </summary>
    public string ColumnName { get; }

    /// <summary>
    /// Optional DbType hint.
    /// </summary>
    public DbType? DbType { get; }

    /// <summary>
    /// Indicates whether the column participates in a key (used for merge/update scenarios).
    /// </summary>
    public bool IsKey { get; }

    /// <summary>
    /// Indicates whether the column is identity. Identity columns are skipped unless KeepIdentity is enabled.
    /// </summary>
    public bool IsIdentity { get; }

    /// <summary>
    /// Indicates whether the destination column accepts NULL.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Provider-specific type name (e.g., PostgreSQL arrays).
    /// </summary>
    public string? ProviderTypeName { get; }
}
