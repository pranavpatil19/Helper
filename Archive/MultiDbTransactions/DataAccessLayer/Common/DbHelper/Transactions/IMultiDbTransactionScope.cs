using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Transactions;

/// <summary>
/// Represents a collection of coordinated transactions spanning multiple databases.
/// </summary>
public interface IMultiDbTransactionScope : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the transaction participants (connection + transaction pairs).
    /// </summary>
    IReadOnlyList<TransactionParticipant> Participants { get; }

    /// <summary>
    /// Commits all participant transactions in order.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits all participant transactions in order.
    /// </summary>
    void Commit();

    /// <summary>
    /// Rolls back all participant transactions.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back all participant transactions.
    /// </summary>
    void Rollback();
}
