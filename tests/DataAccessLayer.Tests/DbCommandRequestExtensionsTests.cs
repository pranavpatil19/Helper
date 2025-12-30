using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;
using DataAccessLayer.Transactions;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class DbCommandRequestExtensionsTests
{
    [Fact]
    public void AsStoredProcedure_UsesExistingName_WhenOverrideMissing()
    {
        var request = new DbCommandRequest
        {
            CommandText = "dbo.GetCustomers"
        };

        var storedProc = request.AsStoredProcedure();

        Assert.Equal(CommandType.StoredProcedure, storedProc.CommandType);
        Assert.Equal("dbo.GetCustomers", storedProc.CommandText);
    }

    [Fact]
    public void AsStoredProcedure_OverridesName_WhenProvided()
    {
        var request = new DbCommandRequest
        {
            CommandText = "dbo.GetCustomers"
        };

        var storedProc = request.AsStoredProcedure("dbo.GetCustomersByRegion");

        Assert.Equal("dbo.GetCustomersByRegion", storedProc.CommandText);
    }

    [Fact]
    public void AsStoredProcedure_Throws_WhenResultingNameIsEmpty()
    {
        var request = new DbCommandRequest { CommandText = string.Empty };
        Assert.Throws<ArgumentException>(() => request.AsStoredProcedure());
    }

    [Fact]
    public void AsStoredProcedure_DefaultsTraceNameToCommandText_WhenMissing()
    {
        var request = new DbCommandRequest
        {
            CommandText = "dbo.GetCustomers"
        };

        var storedProc = request.AsStoredProcedure();

        Assert.Equal("dbo.GetCustomers", storedProc.TraceName);
    }

    [Fact]
    public void AsStoredProcedure_PreservesExistingTraceName()
    {
        var request = new DbCommandRequest
        {
            CommandText = "dbo.GetCustomers",
            TraceName = "custom-trace"
        };

        var storedProc = request.AsStoredProcedure();

        Assert.Equal("custom-trace", storedProc.TraceName);
    }

    [Fact]
    public void WithScope_BindsConnectionAndTransaction()
    {
        var connection = new StubConnection();
        var transaction = new StubTransaction(connection);
        var scope = new StubTransactionScope(connection, transaction);

        var request = new DbCommandRequest { CommandText = "dbo.Anything" };
        var bound = request.WithScope(scope);

        Assert.Same(connection, bound.Connection);
        Assert.Same(transaction, bound.Transaction);
        Assert.False(bound.CloseConnection);
        Assert.Equal("dbo.Anything", bound.TraceName);
    }

    [Fact]
    public void WithScope_HonorsCloseConnectionOverride()
    {
        var connection = new StubConnection();
        var scope = new StubTransactionScope(connection, transaction: null);

        var request = new DbCommandRequest { CommandText = "dbo.Anything" };
        var bound = request.WithScope(scope, closeConnection: true);

        Assert.True(bound.CloseConnection);
    }

    [Fact]
    public void WithScope_PreservesExistingTraceName()
    {
        var connection = new StubConnection();
        var scope = new StubTransactionScope(connection, transaction: null);

        var request = new DbCommandRequest
        {
            CommandText = "dbo.Anything",
            TraceName = "custom-trace"
        };

        var bound = request.WithScope(scope);

        Assert.Equal("custom-trace", bound.TraceName);
    }

    private sealed class StubTransactionScope : ITransactionScope
    {
        public StubTransactionScope(DbConnection connection, DbTransaction? transaction)
        {
            Connection = connection;
            Transaction = transaction;
        }

        public DbConnection Connection { get; }
        public DbTransaction? Transaction { get; }

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Commit() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Rollback() { }
        public Task BeginSavepointAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void BeginSavepoint(string name) { }
        public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RollbackToSavepoint(string name) { }
        public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ReleaseSavepoint(string name) { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }

    private sealed class StubTransaction : DbTransaction
    {
        private readonly DbConnection _connection;

        public StubTransaction(DbConnection connection) => _connection = connection;

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _connection;
        public override void Commit() { }
        public override void Rollback() { }
    }

    private sealed class StubConnection : DbConnection
    {
        private string _connectionString = string.Empty;
        private ConnectionState _state = ConnectionState.Open;

        [AllowNull]
        public override string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = value ?? string.Empty;
        }

        public override string Database => "stub";
        public override string DataSource => "stub";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => _state;
        public override int ConnectionTimeout => 0;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new StubTransaction(this);
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}
