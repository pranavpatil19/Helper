using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Shared.Configuration;

namespace DataAccessLayer.Transactions;

/// <summary>
/// Coordinates transactions that span multiple databases without relying on a distributed transaction coordinator.
/// </summary>
public interface IMultiDbTransactionManager
{
    /// <summary>
    /// Begins coordinated transactions for each database option supplied.
    /// </summary>
    /// <param name="options">Collection of database options (one per participant).</param>
    /// <param name="isolationLevel">Isolation level to use for all participants.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IMultiDbTransactionScope> BeginAsync(
        IReadOnlyList<DatabaseOptions> options,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins coordinated transactions for each database option supplied (sync).
    /// </summary>
    IMultiDbTransactionScope Begin(
        IReadOnlyList<DatabaseOptions> options,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
}
