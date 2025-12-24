using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Transactions;

/// <summary>
/// Represents an ambient database transaction scope with optional savepoint support.
/// </summary>
public interface ITransactionScope : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the connection enlisted in the transaction.
    /// </summary>
    DbConnection Connection { get; }

    /// <summary>
    /// Gets the underlying <see cref="DbTransaction"/>. Null when the scope suppresses transactions.
    /// </summary>
    DbTransaction? Transaction { get; }

    /// <summary>
    /// Commits the transaction asynchronously.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    void Commit();

    /// <summary>
    /// Rolls the transaction back asynchronously.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls the transaction back.
    /// </summary>
    void Rollback();

    /// <summary>
    /// Begins a provider-specific savepoint asynchronously.
    /// </summary>
    Task BeginSavepointAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a provider-specific savepoint.
    /// </summary>
    void BeginSavepoint(string name);

    /// <summary>
    /// Rolls back to the specified savepoint asynchronously.
    /// </summary>
    Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back to the specified savepoint.
    /// </summary>
    void RollbackToSavepoint(string name);

    /// <summary>
    /// Releases the specified savepoint asynchronously (when supported by the provider).
    /// </summary>
    Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the specified savepoint (when supported by the provider).
    /// </summary>
    void ReleaseSavepoint(string name);
}
