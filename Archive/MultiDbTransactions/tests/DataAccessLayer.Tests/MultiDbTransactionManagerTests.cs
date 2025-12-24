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

public sealed class MultiDbTransactionManagerTests
{
    [Fact]
    public async Task BeginAsync_CommitsAllParticipants()
    {
        var factory = new FakeConnectionFactory();
        var manager = CreateManager(factory);
        var options = new[]
        {
            CreateOptions("sql"),
            CreateOptions("pg")
        };

        await using var scope = await manager.BeginAsync(options);
        await scope.CommitAsync();

        foreach (var participant in scope.Participants)
        {
            var fakeConnection = (FakeConnection)participant.Connection;
            Assert.True(fakeConnection.Transaction.Committed);
        }
    }

    [Fact]
    public async Task BeginAsync_WhenCommitFails_RollsBackOthers()
    {
        var factory = new FakeConnectionFactory();
        var manager = CreateManager(factory);
        var options = new[]
        {
            CreateOptions("ok"),
            CreateOptions("fail")
        };

        await using var scope = await manager.BeginAsync(options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.CommitAsync());

        Assert.True(((FakeConnection)scope.Participants[0].Connection).Transaction.RolledBack);
        Assert.True(((FakeConnection)scope.Participants[1].Connection).Transaction.RolledBack);
    }

    private static MultiDbTransactionManager CreateManager(IDbConnectionFactory connectionFactory) =>
        new(connectionFactory, CreateResilienceStrategy(), NullLogger<MultiDbTransactionManager>.Instance, NullLoggerFactory.Instance);

    private static DatabaseOptions CreateOptions(string tag) => new()
    {
        Provider = DatabaseProvider.SqlServer,
        ConnectionString = tag
    };

    private sealed class FakeConnectionFactory : IDbConnectionFactory
    {
        public DbConnection CreateConnection(DatabaseOptions options) =>
            new FakeConnection(options.ConnectionString.Contains("fail", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeConnection : DbConnection
    {
        private readonly bool _failCommit;
        private ConnectionState _state = ConnectionState.Closed;
        public FakeTransaction Transaction { get; private set; } = null!;

        public FakeConnection(bool failCommit) => _failCommit = failCommit;

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;
        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            Open();
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            Transaction = new FakeTransaction(this, _failCommit);
            return Transaction;
        }

        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    private sealed class FakeTransaction : DbTransaction
    {
        private readonly DbConnection _connection;
        private readonly bool _failCommit;

        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }

        public FakeTransaction(DbConnection connection, bool failCommit)
        {
            _connection = connection;
            _failCommit = failCommit;
        }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _connection;

        public override void Commit()
        {
            if (_failCommit)
            {
                RolledBack = true;
                throw new InvalidOperationException("Commit failed.");
            }

            Committed = true;
        }

        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Commit();
            return Task.CompletedTask;
        }

        public override void Rollback()
        {
            RolledBack = true;
        }

        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollback();
            return Task.CompletedTask;
        }
    }

    private static IResilienceStrategy CreateResilienceStrategy() =>
        new ResilienceStrategy(
            new ResilienceOptions
            {
                EnableCommandRetries = false,
                EnableTransactionRetries = true,
                TransactionRetryCount = 1,
                TransactionBaseDelayMilliseconds = 1
            },
            NullLogger<ResilienceStrategy>.Instance);
}
