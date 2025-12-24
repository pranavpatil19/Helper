using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Provider-specific executor for bulk operations.
/// </summary>
public interface IBulkEngine
{
    /// <summary>
    /// Gets the database provider this engine supports.
    /// </summary>
    DatabaseProvider Provider { get; }

    /// <summary>
    /// Executes the supplied operation using provider-optimized primitives.
    /// </summary>
    /// <typeparam name="T">Entity type.</typeparam>
    /// <param name="operation">Bulk operation metadata.</param>
    /// <param name="rows">Materialized rows to write.</param>
    /// <param name="providerOptions">Effective provider options (global or overridden per operation).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<BulkExecutionResult> ExecuteAsync<T>(
        BulkOperation<T> operation,
        IReadOnlyList<T> rows,
        DatabaseOptions providerOptions,
        CancellationToken cancellationToken);
}
