using System;
using System.Collections.Generic;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Options controlling how <see cref="PostgresBulkWriter{T}"/> performs COPY operations.
/// </summary>
/// <typeparam name="T">Row type.</typeparam>
public sealed class PostgresBulkWriterOptions<T>
{
    /// <summary>
    /// Gets or sets the destination table (required when <see cref="CopyCommand"/> is not provided).
    /// </summary>
    public string? DestinationTable { get; set; }

    /// <summary>
    /// Gets the column names written by the COPY command.
    /// </summary>
    public IReadOnlyList<string> ColumnNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets a fully customized COPY command. When provided, <see cref="DestinationTable"/> and <see cref="ColumnNames"/> are only used for validation.
    /// </summary>
    public string? CopyCommand { get; set; }

    /// <summary>
    /// Gets or sets the delegate that converts a row into an array matching <see cref="ColumnNames"/>.
    /// </summary>
    public Func<T, object?[]>? ValueSelector { get; set; }

    /// <summary>
    /// Gets or sets optional column metadata for type-aware COPY writes.
    /// </summary>
    public IReadOnlyList<BulkColumn>? Columns { get; set; }

    /// <summary>
    /// Gets or sets provider overrides for the connection factory.
    /// </summary>
    public DatabaseOptions? OverrideOptions { get; set; }
}
