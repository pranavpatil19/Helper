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

namespace DataAccessLayer.Tests.Integration;

public sealed class MultiDbTransactionManagerTests
{
    [Fact]
    public async Task BeginAsync_CreatesParticipants_ForAllOptions()
    {
        var factory = new RecordingConnectionFactory();
        var manager = CreateManager(factory);
        var options = new[]
        {
            new DatabaseOptions { Provider = DatabaseProvider.SqlServer, ConnectionString = "one" },
            new DatabaseOptions { Provider = DatabaseProvider.PostgreSql, ConnectionString = "two" }
        };

        await using var scope = await manager.BeginAsync(options);

        Assert.Equal(2, scope.Participants.Count);
        Assert.Equal(2, factory.CreatedConnections.Count);
    }

    [Fact]
    public async Task CommitAsync_CommitsEveryParticipant()
    {
        var factory = new RecordingConnectionFactory();
        var manager = CreateManager(factory);
        var options = new[]
        {
            new DatabaseOptions { Provider = DatabaseProvider.SqlServer, ConnectionString = "one" },
            new DatabaseOptions { Provider = DatabaseProvider.PostgreSql, ConnectionString = "two" }
        };

        await using var scope = await manager.BeginAsync(options);
        await scope.CommitAsync();

        Assert.All(scope.Participants, participant => Assert.True(((RecordingTransaction)participant.Transaction).Committed));
    }

    private static MultiDbTransactionManager CreateManager(IDbConnectionFactory factory)
    {
        var resilience = new ResilienceStrategy(new ResilienceOptions(), NullLogger<ResilienceStrategy>.Instance);
        return new MultiDbTransactionManager(
            factory,
            resilience,
            NullLogger<MultiDbTransactionManager>.Instance,
            NullLoggerFactory.Instance);
    }

    private sealed class RecordingConnectionFactory : IDbConnectionFactory
    {
        public List<DbConnection> CreatedConnections { get; } = new();
        public DbConnection CreateConnection(DatabaseOptions options)
        {
            var connection = new RecordingConnection();
            CreatedConnections.Add(connection);
            return connection;
        }
    }

    private sealed class RecordingConnection : DbConnection
    {
        public bool Opened { get; private set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => Opened ? ConnectionState.Open : ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => Opened = false;
        public override void Open() => Opened = true;
        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            Open();
            return Task.CompletedTask;
        }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new RecordingTransaction(this);
        protected override DbCommand CreateDbCommand() => throw new System.NotSupportedException();
    }

    private sealed class RecordingTransaction : DbTransaction
    {
        private readonly DbConnection _connection;
        public bool Committed { get; private set; }
        public RecordingTransaction(DbConnection connection) => _connection = connection;
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _connection;
        public override void Commit() => Committed = true;
        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Commit();
            return Task.CompletedTask;
        }
        public override void Rollback() { }
    }
}
