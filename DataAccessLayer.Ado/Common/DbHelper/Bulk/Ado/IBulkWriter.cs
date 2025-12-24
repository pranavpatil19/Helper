using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Defines a provider-specific high-throughput bulk writer (SqlBulkCopy, COPY, array-bind, etc.).
/// </summary>
/// <typeparam name="T">Type of row being written.</typeparam>
public interface IBulkWriter<in T>
{
    /// <summary>
    /// Writes the specified rows synchronously.
    /// </summary>
    void Write(IEnumerable<T> rows);

    /// <summary>
    /// Writes the specified rows asynchronously.
    /// </summary>
    Task WriteAsync(IEnumerable<T> rows, CancellationToken cancellationToken = default);
}
