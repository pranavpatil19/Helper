using Microsoft.Data.SqlClient;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Common options influencing provider-specific bulk writers.
/// </summary>
public sealed class BulkOptions
{
    /// <summary>
    /// Keeps identity values when providers support it (SqlBulkCopy KeepIdentity).
    /// </summary>
    public bool KeepIdentity { get; init; }

    /// <summary>
    /// Requests table-level locks during bulk copy.
    /// </summary>
    public bool UseTableLock { get; init; }

    /// <summary>
    /// Batch size hint for writers that support chunking (Oracle array bind, SqlBulkCopy batch size).
    /// </summary>
    public int? BatchSize { get; init; }

    /// <summary>
    /// Optional provider override for the connection factory (falls back to global DatabaseOptions).
    /// </summary>
    public DatabaseOptions? OverrideOptions { get; init; }

    /// <summary>
    /// Allows callers to specify the precise SqlBulkCopyOptions mask; the helper augments it with other flags.
    /// </summary>
    public SqlBulkCopyOptions? SqlServerOptions { get; init; }
}
