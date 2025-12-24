using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// High-level abstraction for executing provider-aware bulk operations.
/// </summary>
public interface IBulkWriteHelper
{
    Task<BulkExecutionResult> ExecuteAsync<T>(
        BulkOperation<T> operation,
        IReadOnlyCollection<T> rows,
        CancellationToken cancellationToken = default);

    BulkExecutionResult Execute<T>(
        BulkOperation<T> operation,
        IReadOnlyCollection<T> rows);
}
