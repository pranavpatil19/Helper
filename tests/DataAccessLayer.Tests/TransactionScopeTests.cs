using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Transactions;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class TransactionScopeTests
{
    [Fact]
    public async Task DisposeAsync_RollsBack_WhenNotCommitted()
    {
        var (scope, transaction) = CreateScope();

        await scope.DisposeAsync();

        Assert.True(transaction.RolledBack);
    }

    [Fact]
    public void BeginSavepoint_DelegatesToSavepointManager()
    {
        var savepointManager = new RecordingSavepointManager();
        var (scope, _) = CreateScope(savepointManager);

        scope.BeginSavepoint("sp1");
        scope.RollbackToSavepoint("sp1");
        scope.ReleaseSavepoint("sp1");

        Assert.Equal(new[] { "Begin:sp1", "Rollback:sp1", "Release:sp1" }, savepointManager.Events);
    }

    private static (TransactionScope Scope, RecordingTransaction Transaction) CreateScope(ISavepointManager? savepointManager = null)
    {
        savepointManager ??= new RecordingSavepointManager();
        var resilience = new ResilienceStrategy(new ResilienceOptions(), NullLogger<ResilienceStrategy>.Instance);
        var options = new DatabaseOptions { Provider = DatabaseProvider.SqlServer, ConnectionString = "Server=.;Database=Fake;" };
        var connection = new StubConnection();
        var transaction = new RecordingTransaction(connection);
        var scope = new TransactionScope(connection, transaction, options, savepointManager, resilience, NullLogger<TransactionScope>.Instance);
        return (scope, transaction);
    }

    private sealed class StubConnection : DbConnection
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new RecordingTransaction(this);
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    private sealed class RecordingTransaction : DbTransaction
    {
        private readonly DbConnection _connection;
        public bool RolledBack { get; private set; }
        public RecordingTransaction(DbConnection connection) => _connection = connection;
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _connection;
        public override void Commit() => RolledBack = false;
        public override Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override void Rollback() => RolledBack = true;
        public override Task RollbackAsync(CancellationToken cancellationToken = default) { RolledBack = true; return Task.CompletedTask; }
    }

    private sealed class RecordingSavepointManager : ISavepointManager
    {
        public List<string> Events { get; } = new();
        public Task BeginSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default)
        {
            Events.Add($"Begin:{name}");
            return Task.CompletedTask;
        }
        public void BeginSavepoint(DbTransaction transaction, string name, DatabaseOptions options) => Events.Add($"Begin:{name}");
        public Task RollbackToSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default)
        {
            Events.Add($"Rollback:{name}");
            return Task.CompletedTask;
        }
        public void RollbackToSavepoint(DbTransaction transaction, string name, DatabaseOptions options) => Events.Add($"Rollback:{name}");
        public Task ReleaseSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default)
        {
            Events.Add($"Release:{name}");
            return Task.CompletedTask;
        }
        public void ReleaseSavepoint(DbTransaction transaction, string name, DatabaseOptions options) => Events.Add($"Release:{name}");
    }
}
