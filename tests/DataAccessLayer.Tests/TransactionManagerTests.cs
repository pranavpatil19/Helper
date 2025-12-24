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

public sealed class TransactionManagerTests
{
    [Fact]
    public async Task BeginAsync_OpensConnection_AndBeginsTransaction()
    {
        var connection = new TestConnection();
        var manager = CreateManager(connection);

        await using var scope = await manager.BeginAsync();

        Assert.True(connection.Opened);
        Assert.NotNull(scope);
        Assert.NotNull(TransactionScopeAmbient.Current);
    }

    [Fact]
    public void Begin_ReturnsScope_WithProvidedOptions()
    {
        var connection = new TestConnection();
        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.PostgreSql,
            ConnectionString = "Host=localhost;"
        };
        var manager = CreateManager(connection);

        using var scope = manager.Begin(options: options);

        Assert.Same(connection.LastTransaction?.Connection, scope.Connection);
    }

    [Fact]
    public void Begin_RequiredWithinAmbient_ReusesTransaction()
    {
        var connection = new TestConnection();
        var manager = CreateManager(connection);

        using var outer = manager.Begin();
        using var inner = manager.Begin(scopeOption: TransactionScopeOption.Required);

        Assert.Same(outer.Connection, inner.Connection);
        Assert.Same(outer.Transaction, inner.Transaction);
    }

    [Fact]
    public void Begin_RequiresNew_CreatesIndependentTransaction()
    {
        var factory = new CountingConnectionFactory();
        var manager = CreateManager(factory);

        using var outer = manager.Begin();
        using var inner = manager.Begin(scopeOption: TransactionScopeOption.RequiresNew);

        Assert.NotSame(outer.Connection, inner.Connection);
        Assert.NotSame(outer.Transaction, inner.Transaction);
        Assert.Equal(2, factory.Connections.Count);
    }

    [Fact]
    public void Begin_Suppress_ProvidesConnectionWithoutTransaction()
    {
        var factory = new CountingConnectionFactory();
        var manager = CreateManager(factory);

        using var scope = manager.Begin(scopeOption: TransactionScopeOption.Suppress);

        Assert.NotNull(scope.Connection);
        Assert.Null(scope.Transaction);
    }

    [Fact]
    public async Task BeginAsync_RequiredWithinAmbient_ReusesTransaction()
    {
        var connection = new TestConnection();
        var manager = CreateManager(connection);

        await using var outer = await manager.BeginAsync();
        await using var inner = await manager.BeginAsync(scopeOption: TransactionScopeOption.Required);

        Assert.Same(outer.Connection, inner.Connection);
        Assert.Same(outer.Transaction, inner.Transaction);

        await inner.CommitAsync();
        Assert.False(connection.LastTransaction?.Committed ?? false);

        await outer.CommitAsync();
        Assert.True(connection.LastTransaction?.Committed ?? false);
    }

    [Fact]
    public async Task BeginAsync_RequiresNew_UsesIndependentConnection()
    {
        var factory = new CountingConnectionFactory();
        var manager = CreateManager(factory);

        await using var outer = await manager.BeginAsync();
        await using var inner = await manager.BeginAsync(scopeOption: TransactionScopeOption.RequiresNew);

        Assert.NotSame(outer.Connection, inner.Connection);
        Assert.NotSame(outer.Transaction, inner.Transaction);
        Assert.Equal(2, factory.Connections.Count);
    }

    [Fact]
    public async Task BeginAsync_Suppress_ProvidesConnectionWithoutTransaction()
    {
        var factory = new CountingConnectionFactory();
        var manager = CreateManager(factory);

        await using var scope = await manager.BeginAsync(scopeOption: TransactionScopeOption.Suppress);

        Assert.NotNull(scope.Connection);
        Assert.Null(scope.Transaction);
        await scope.CommitAsync();
    }

    private static TransactionManager CreateManager(DbConnection connection)
        => CreateManager(new StubConnectionFactory(connection));

    private static TransactionManager CreateManager(IDbConnectionFactory factory)
    {
        var dbOptions = new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=.;Database=Fake;Trusted_Connection=True;"
        };

        return new TransactionManager(
            factory,
            new StubSavepointManager(),
            dbOptions,
            new ResilienceStrategy(dbOptions.Resilience, NullLogger<ResilienceStrategy>.Instance),
            NullLogger<TransactionManager>.Instance,
            NullLoggerFactory.Instance);
    }

    private sealed class StubConnectionFactory : IDbConnectionFactory
    {
        private readonly DbConnection _connection;
        public StubConnectionFactory(DbConnection connection) => _connection = connection;
        public DbConnection CreateConnection(DatabaseOptions options) => _connection;
    }

    private sealed class CountingConnectionFactory : IDbConnectionFactory
    {
        public List<TestConnection> Connections { get; } = new();
        public DbConnection CreateConnection(DatabaseOptions options)
        {
            var connection = new TestConnection();
            Connections.Add(connection);
            return connection;
        }
    }

    private sealed class StubSavepointManager : ISavepointManager
    {
        public Task BeginSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void BeginSavepoint(DbTransaction transaction, string name, DatabaseOptions options) { }
        public Task RollbackToSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RollbackToSavepoint(DbTransaction transaction, string name, DatabaseOptions options) { }
        public Task ReleaseSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ReleaseSavepoint(DbTransaction transaction, string name, DatabaseOptions options) { }
    }

    private sealed class TestConnection : DbConnection
    {
        public bool Opened { get; private set; }
        public TestTransaction? LastTransaction { get; private set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => Opened ? ConnectionState.Open : ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => Opened = false;
        public override void Open() => Opened = true;
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            var transaction = new TestTransaction(this);
            LastTransaction = transaction;
            return transaction;
        }
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    private sealed class TestTransaction : DbTransaction
    {
        private readonly DbConnection _connection;
        public bool Committed { get; private set; }
        public TestTransaction(DbConnection connection) => _connection = connection;
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _connection;
        public override void Commit() => Committed = true;
        public override void Rollback() { }
    }
}
