using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Transactions;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Manages connection/transaction lifetimes for DAL helpers, reusing ambient scopes when available.
/// </summary>
public interface IConnectionScopeManager
{
    ConnectionScope Lease(DatabaseOptions? overrideOptions = null);
    Task<ConnectionScope> LeaseAsync(DatabaseOptions? overrideOptions = null, CancellationToken cancellationToken = default);
}

public sealed class ConnectionScopeManager : IConnectionScopeManager
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly DatabaseOptions _defaultOptions;

    public ConnectionScopeManager(IDbConnectionFactory connectionFactory, DatabaseOptions defaultOptions)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _defaultOptions = defaultOptions ?? throw new ArgumentNullException(nameof(defaultOptions));
    }

    public ConnectionScope Lease(DatabaseOptions? overrideOptions = null)
    {
        var ambient = TransactionScopeAmbient.Current;
        if (ambient is not null)
        {
            return ConnectionScope.Reuse(ambient.Connection, ambient.Transaction);
        }

        var options = overrideOptions ?? _defaultOptions;
        var connection = _connectionFactory.CreateConnection(options);
        connection.Open();
        return ConnectionScope.Create(connection, ownsConnection: true);
    }

    public async Task<ConnectionScope> LeaseAsync(DatabaseOptions? overrideOptions = null, CancellationToken cancellationToken = default)
    {
        var ambient = TransactionScopeAmbient.Current;
        if (ambient is not null)
        {
            return ConnectionScope.Reuse(ambient.Connection, ambient.Transaction);
        }

        var options = overrideOptions ?? _defaultOptions;
        var connection = _connectionFactory.CreateConnection(options);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return ConnectionScope.Create(connection, ownsConnection: true);
    }
}

public sealed class ConnectionScope : IAsyncDisposable, IDisposable
{
    private readonly bool _disposeConnection;
    private bool _disposed;

    private ConnectionScope(DbConnection connection, bool disposeConnection, DbTransaction? transaction)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Transaction = transaction;
        _disposeConnection = disposeConnection;
    }

    public DbConnection Connection { get; }

    public DbTransaction? Transaction { get; }

    public static ConnectionScope Create(DbConnection connection, bool ownsConnection) =>
        new(connection, ownsConnection, null);

    public static ConnectionScope Reuse(DbConnection connection, DbTransaction? transaction) =>
        new(connection, false, transaction);

    public static ConnectionScope Wrap(DbConnection connection, DbTransaction? transaction, bool disposeConnection) =>
        new(connection, disposeConnection, transaction);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_disposeConnection)
        {
            Connection.Dispose();
        }

        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_disposeConnection)
        {
            await Connection.DisposeAsync().ConfigureAwait(false);
        }

        _disposed = true;
    }
}
