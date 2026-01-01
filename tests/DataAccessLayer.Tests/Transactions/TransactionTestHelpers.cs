using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Transactions;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Configuration;

namespace DataAccessLayer.Tests.Transactions;

internal static class TransactionTestHelpers
{
    public static TransactionManager CreateManager(
        IDbConnectionFactory factory,
        ISavepointManager savepointManager)
    {
        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=.;Database=Fake;Trusted_Connection=True;"
        };

        return new TransactionManager(
            factory,
            savepointManager,
            options,
            new ResilienceStrategy(options.Resilience, NullLogger<ResilienceStrategy>.Instance),
            NullLogger<TransactionManager>.Instance,
            NullLoggerFactory.Instance);
    }
}

internal sealed class RecordingConnectionFactory : IDbConnectionFactory
{
    public List<RecordingConnection> Connections { get; } = new();

    public DbConnection CreateConnection(DatabaseOptions options)
    {
        var connection = new RecordingConnection();
        Connections.Add(connection);
        return connection;
    }
}

internal sealed class RecordingConnection : DbConnection
{
    public bool IsOpen { get; private set; }
    public bool IsDisposed { get; private set; }
    public RecordingTransaction? LastTransaction { get; private set; }

    [AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => "Fake";
    public override string DataSource => "Fake";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => IsOpen ? ConnectionState.Open : ConnectionState.Closed;

    public override void ChangeDatabase(string databaseName) { }

    public override void Close() => IsOpen = false;

    public override void Open() => IsOpen = true;

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        Open();
        return Task.CompletedTask;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        LastTransaction = new RecordingTransaction(this, isolationLevel);
        return LastTransaction;
    }

    protected override DbCommand CreateDbCommand() => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

internal sealed class RecordingTransaction : DbTransaction
{
    private readonly RecordingConnection _connection;

    public RecordingTransaction(RecordingConnection connection, IsolationLevel isolationLevel)
    {
        _connection = connection;
        IsolationLevel = isolationLevel;
    }

    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }

    public override IsolationLevel IsolationLevel { get; }

    protected override DbConnection DbConnection => _connection;

    public override void Commit() => Committed = true;

    public override Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Commit();
        return Task.CompletedTask;
    }

    public override void Rollback() => RolledBack = true;

    public override Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        Rollback();
        return Task.CompletedTask;
    }
}

internal sealed class RecordingSavepointManager : ISavepointManager
{
    public List<string> Events { get; } = new();

    public Task BeginSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default)
    {
        Events.Add($"Begin:{name}");
        return Task.CompletedTask;
    }

    public void BeginSavepoint(DbTransaction transaction, string name, DatabaseOptions options) =>
        Events.Add($"Begin:{name}");

    public Task RollbackToSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default)
    {
        Events.Add($"Rollback:{name}");
        return Task.CompletedTask;
    }

    public void RollbackToSavepoint(DbTransaction transaction, string name, DatabaseOptions options) =>
        Events.Add($"Rollback:{name}");

    public Task ReleaseSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default)
    {
        Events.Add($"Release:{name}");
        return Task.CompletedTask;
    }

    public void ReleaseSavepoint(DbTransaction transaction, string name, DatabaseOptions options) =>
        Events.Add($"Release:{name}");
}
