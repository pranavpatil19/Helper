using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using Shared.Configuration;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Transactions;

internal sealed class SuppressedTransactionScope : ITransactionScope, IAmbientScope
{
    private readonly DbConnection _connection;
    private IDisposable? _ambientToken;
    private bool _disposed;

    private SuppressedTransactionScope(DbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public static async Task<SuppressedTransactionScope> CreateAsync(
        IDbConnectionFactory connectionFactory,
        DatabaseOptions options,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection(options);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return new SuppressedTransactionScope(connection);
    }

    public static SuppressedTransactionScope Create(
        IDbConnectionFactory connectionFactory,
        DatabaseOptions options)
    {
        var connection = connectionFactory.CreateConnection(options);
        connection.Open();
        return new SuppressedTransactionScope(connection);
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
    public DbTransaction? Transaction => null;

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Commit() { }
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Rollback() { }

    public Task BeginSavepointAsync(string name, CancellationToken cancellationToken = default) =>
        throw new TransactionFeatureNotSupportedException("Savepoints are not supported when transactions are suppressed.");

    public void BeginSavepoint(string name) =>
        throw new TransactionFeatureNotSupportedException("Savepoints are not supported when transactions are suppressed.");

    public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default) =>
        throw new TransactionFeatureNotSupportedException("Savepoints are not supported when transactions are suppressed.");

    public void RollbackToSavepoint(string name) =>
        throw new TransactionFeatureNotSupportedException("Savepoints are not supported when transactions are suppressed.");

    public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default) =>
        throw new TransactionFeatureNotSupportedException("Savepoints are not supported when transactions are suppressed.");

    public void ReleaseSavepoint(string name) =>
        throw new TransactionFeatureNotSupportedException("Savepoints are not supported when transactions are suppressed.");

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _connection.DisposeAsync().ConfigureAwait(false);
        _ambientToken?.Dispose();
        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _connection.Dispose();
        _ambientToken?.Dispose();
        _disposed = true;
    }
}
