using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Transactions;

internal sealed class DependentTransactionScope : ITransactionScope
{
    private readonly ITransactionScope _parent;
    private bool _completed;
    private bool _disposed;

    public DependentTransactionScope(ITransactionScope parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    public DbConnection Connection => _parent.Connection;
    public DbTransaction? Transaction => _parent.Transaction;

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        _completed = true;
        return Task.CompletedTask;
    }

    public void Commit() => _completed = true;

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        _parent.Rollback();
        _completed = true;
        return Task.CompletedTask;
    }

    public void Rollback()
    {
        _parent.Rollback();
        _completed = true;
    }

    public Task BeginSavepointAsync(string name, CancellationToken cancellationToken = default) =>
        _parent.BeginSavepointAsync(name, cancellationToken);

    public void BeginSavepoint(string name) => _parent.BeginSavepoint(name);

    public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default) =>
        _parent.RollbackToSavepointAsync(name, cancellationToken);

    public void RollbackToSavepoint(string name) => _parent.RollbackToSavepoint(name);

    public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default) =>
        _parent.ReleaseSavepointAsync(name, cancellationToken);

    public void ReleaseSavepoint(string name) => _parent.ReleaseSavepoint(name);

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_completed)
        {
            _parent.Rollback();
        }

        _disposed = true;
    }
}
