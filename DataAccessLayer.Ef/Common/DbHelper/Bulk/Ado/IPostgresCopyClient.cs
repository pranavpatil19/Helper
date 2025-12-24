using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Abstraction over Npgsql binary COPY operations for bulk inserts.
/// </summary>
public interface IPostgresCopyClient : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Configures optional column metadata for type-aware writes.
    /// </summary>
    void ConfigureColumns(IReadOnlyList<BulkColumn>? columns);

    /// <summary>
    /// Writes a single row to the COPY stream.
    /// </summary>
    void WriteRow(object?[] values);

    /// <summary>
    /// Writes a single row to the COPY stream asynchronously.
    /// </summary>
    Task WriteRowAsync(object?[] values, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the COPY operation.
    /// </summary>
    void Complete();

    /// <summary>
    /// Completes the COPY operation asynchronously.
    /// </summary>
    Task CompleteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates <see cref="IPostgresCopyClient"/> instances for a given connection.
/// </summary>
public interface IPostgresCopyClientFactory
{
    /// <summary>
    /// Creates a COPY client bound to the specified connection/command text.
    /// </summary>
    IPostgresCopyClient Create(DbConnection connection, string copyCommand);
}
