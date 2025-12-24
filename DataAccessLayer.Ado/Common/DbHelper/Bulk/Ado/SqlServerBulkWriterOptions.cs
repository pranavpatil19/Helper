using System;
using System.Collections.Generic;
using System.Data.Common;
using DataAccessLayer.Execution;
using Microsoft.Data.SqlClient;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

public sealed class SqlServerBulkWriterOptions<T>
{
    public string DestinationTable { get; init; } = string.Empty;
    public IReadOnlyList<string> ColumnNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<BulkColumn>? Columns { get; init; }
    public Func<T, object?[]> ValueSelector { get; init; } = _ => Array.Empty<object?>();
    public DatabaseOptions? OverrideOptions { get; init; }
    public SqlBulkCopyOptions BulkCopyOptions { get; init; } = SqlBulkCopyOptions.Default;
    public int? BatchSize { get; init; }
    public int? BulkCopyTimeoutSeconds { get; init; }
}
