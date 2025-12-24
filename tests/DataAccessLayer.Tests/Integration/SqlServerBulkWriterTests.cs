using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using DataAccessLayer.Common.DbHelper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace DataAccessLayer.Tests.Integration;

public sealed class SqlServerBulkWriterTests
{
    [Fact]
    public async Task WriteAsync_ForwardsRowsToClient()
    {
        var client = new RecordingSqlBulkCopyClient();
        var factory = new RecordingSqlBulkCopyClientFactory(client);
        var writer = new SqlServerBulkWriter<BulkRow>(
            () => new FakeConnection(),
            new SqlServerBulkWriterOptions<BulkRow>
            {
                DestinationTable = "dbo.Rows",
                ColumnNames = new[] { "Id", "Name" },
                ValueSelector = row => new object?[] { row.Id, row.Name }
            },
            factory);

        await writer.WriteAsync(new[]
        {
            new BulkRow(1, "A"),
            new BulkRow(2, "B")
        });

        Assert.Equal("dbo.Rows", client.DestinationTableName);
        Assert.Equal(2, client.Rows.Count);
        Assert.Equal((2, "B"), client.Rows[1]);
    }

    private sealed record BulkRow(int Id, string Name);

    private sealed class RecordingSqlBulkCopyClientFactory : ISqlBulkCopyClientFactory
    {
        private readonly RecordingSqlBulkCopyClient _client;
        public RecordingSqlBulkCopyClientFactory(RecordingSqlBulkCopyClient client) => _client = client;
        public ISqlBulkCopyClient Create(DbConnection connection, SqlBulkCopyOptions options, DbTransaction? transaction = null) => _client;
    }

    private sealed class RecordingSqlBulkCopyClient : ISqlBulkCopyClient
    {
        public List<(int Id, string Name)> Rows { get; } = new();
        public string DestinationTableName { get; set; } = string.Empty;
        public int BatchSize { get; set; }
        public int BulkCopyTimeout { get; set; }
        public void AddColumnMapping(string sourceColumn, string destinationColumn) { }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void WriteToServer(IDataReader reader)
        {
            while (reader.Read())
            {
                Rows.Add(((int)reader.GetValue(0), (string)reader.GetValue(1)));
            }
        }
        public Task WriteToServerAsync(IDataReader reader, CancellationToken cancellationToken = default)
        {
            WriteToServer(reader);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConnection : DbConnection
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
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new System.NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new System.NotSupportedException();
    }
}
