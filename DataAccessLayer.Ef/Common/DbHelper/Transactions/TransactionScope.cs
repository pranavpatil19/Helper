using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using Microsoft.Extensions.Logging;
using Polly;
using Shared.Configuration;

namespace DataAccessLayer.Transactions;

internal sealed class TransactionScope : ITransactionScope, IAmbientScope
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private readonly DatabaseOptions _options;
    private readonly ISavepointManager _savepointManager;
    private readonly IResilienceStrategy _resilience;
    private readonly ILogger<TransactionScope> _logger;
    private IDisposable? _ambientToken;
    private bool _committed;
    private bool _disposed;

    public TransactionScope(
        DbConnection connection,
        DbTransaction transaction,
        DatabaseOptions options,
        ISavepointManager savepointManager,
        IResilienceStrategy resilience,
        ILogger<TransactionScope> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _savepointManager = savepointManager ?? throw new ArgumentNullException(nameof(savepointManager));
        _resilience = resilience ?? throw new ArgumentNullException(nameof(resilience));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void EnterAmbient()
    {
        if (_ambientToken is not null)
        {
            return;
        }

        _ambientToken = TransactionScopeAmbient.Push(this);
    }

    public DbConnection Connection => _connection;
    public DbTransaction Transaction => _transaction;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await _resilience.TransactionAsyncPolicy.ExecuteAsync((_, token) => _transaction.CommitAsync(token), new Context(), cancellationToken)
            .ConfigureAwait(false);
        _committed = true;
    }

    public void Commit()
    {
        EnsureNotDisposed();
        _resilience.TransactionSyncPolicy.Execute(_transaction.Commit);
        _committed = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (_committed)
        {
            return;
        }

        await _resilience.TransactionAsyncPolicy.ExecuteAsync((_, token) => _transaction.RollbackAsync(token), new Context(), cancellationToken)
            .ConfigureAwait(false);
        _committed = true;
    }

    public void Rollback()
    {
        EnsureNotDisposed();
        if (_committed)
        {
            return;
        }

        _resilience.TransactionSyncPolicy.Execute(_transaction.Rollback);
        _committed = true;
    }

    public Task BeginSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        return _savepointManager.BeginSavepointAsync(_transaction, name, _options, cancellationToken);
    }

    public void BeginSavepoint(string name)
    {
        EnsureNotDisposed();
        _savepointManager.BeginSavepoint(_transaction, name, _options);
    }

    public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        return _savepointManager.RollbackToSavepointAsync(_transaction, name, _options, cancellationToken);
    }

    public void RollbackToSavepoint(string name)
    {
        EnsureNotDisposed();
        _savepointManager.RollbackToSavepoint(_transaction, name, _options);
    }

    public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        return _savepointManager.ReleaseSavepointAsync(_transaction, name, _options, cancellationToken);
    }

    public void ReleaseSavepoint(string name)
    {
        EnsureNotDisposed();
        _savepointManager.ReleaseSavepoint(_transaction, name, _options);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!_committed)
            {
                _logger.LogWarning("Transaction disposed without explicit commit; rolling back.");
                await _transaction.RollbackAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
            _ambientToken?.Dispose();
            _disposed = true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!_committed)
            {
                _logger.LogWarning("Transaction disposed without explicit commit; rolling back.");
                _transaction.Rollback();
            }
        }
        finally
        {
            _transaction.Dispose();
            _connection.Dispose();
            _ambientToken?.Dispose();
            _disposed = true;
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TransactionScope));
        }
    }
}
