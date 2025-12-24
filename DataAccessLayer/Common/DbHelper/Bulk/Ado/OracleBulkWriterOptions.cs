using System;
using System.Collections.Generic;
using System.Data;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Options for Oracle array-binding bulk writes.
/// </summary>
/// <typeparam name="T">Row type.</typeparam>
public sealed class OracleBulkWriterOptions<T>
{
    /// <summary>
    /// Gets or sets the command text executed for each batch (typically an INSERT or package procedure).
    /// </summary>
    public string CommandText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command type (Text or StoredProcedure).
    /// </summary>
    public CommandType CommandType { get; set; } = CommandType.Text;

    /// <summary>
    /// Gets or sets the logical parameter names used when binding arrays.
    /// </summary>
    public IReadOnlyList<string> ParameterNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets optional DbType hints per parameter.
    /// </summary>
    public IReadOnlyList<DbType?>? ParameterDbTypes { get; set; }
        = Array.Empty<DbType?>();

    /// <summary>
    /// Gets or sets the maximum number of rows per batch (defaults to 256).
    /// </summary>
    public int BatchSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the delegate that converts a row to values matching <see cref="ParameterNames"/>.
    /// </summary>
    public Func<T, object?[]>? ValueSelector { get; set; }
        = null;

    /// <summary>
    /// Gets or sets connection overrides.
    /// </summary>
    public DatabaseOptions? OverrideOptions { get; set; }
}
