using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using Microsoft.Data.SqlClient;

namespace Data.Benchmarks;

[MemoryDiagnoser]
public class BulkWriteBenchmarks
{
    private readonly SqlServerBulkWriter<BulkRow> _writer;
    private readonly List<BulkRow> _rows;

    public BulkWriteBenchmarks()
    {
        var client = new NoOpSqlBulkCopyClient();
        _writer = new SqlServerBulkWriter<BulkRow>(
            () => new FakeConnection(),
            new SqlServerBulkWriterOptions<BulkRow>
            {
                DestinationTable = "dbo.BulkRows",
                ColumnNames = new[]
                {
                    "Id", "Name", "CreatedUtc", "Amount", "IsActive",
                    "ReferenceId", "Score", "Rating", "Sequence", "Notes"
                },
                ValueSelector = row => new object?[]
                {
                    row.Id,
                    row.Name,
                    row.CreatedUtc,
                    row.Amount,
                    row.IsActive,
                    row.ReferenceId,
                    row.Score,
                    row.Rating,
                    row.Sequence,
                    row.Notes
                }
            },
            new NoOpSqlBulkCopyClientFactory(client));

        _rows = new List<BulkRow>();
        for (var i = 0; i < 100_000; i++)
        {
            _rows.Add(new BulkRow(
                Id: i,
                Name: $"Row {i}",
                CreatedUtc: DateTime.UtcNow.AddMinutes(-i),
                Amount: i * 1.5m,
                IsActive: i % 2 == 0,
                ReferenceId: Guid.NewGuid(),
                Score: i / 10.0,
                Rating: (short)(i % 100),
                Sequence: 1_000_000L + i,
                Notes: $"Note {i}"));
        }
    }

    [Benchmark]
    public void Write() => _writer.Write(_rows);

    [Benchmark]
    public System.Threading.Tasks.Task WriteAsync() => _writer.WriteAsync(_rows);

    private sealed record BulkRow(
        int Id,
        string Name,
        DateTime CreatedUtc,
        decimal Amount,
        bool IsActive,
        Guid ReferenceId,
        double Score,
        short Rating,
        long Sequence,
        string Notes);

#pragma warning disable CS8765

    private sealed class NoOpSqlBulkCopyClientFactory : ISqlBulkCopyClientFactory
    {
        private readonly ISqlBulkCopyClient _client;
        public NoOpSqlBulkCopyClientFactory(ISqlBulkCopyClient client) => _client = client;
        public ISqlBulkCopyClient Create(DbConnection connection, SqlBulkCopyOptions options, DbTransaction? transaction = null) => _client;
    }

    private sealed class NoOpSqlBulkCopyClient : ISqlBulkCopyClient
    {
        public string DestinationTableName { get; set; } = string.Empty;
        public int BatchSize { get; set; }
        public int BulkCopyTimeout { get; set; }
        public void AddColumnMapping(string sourceColumn, string destinationColumn) => _ = (sourceColumn, destinationColumn);
        public void Dispose() => GC.SuppressFinalize(this);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void WriteToServer(IDataReader reader)
        {
            while (reader.Read())
            {
                _ = reader.FieldCount;
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
#pragma warning restore CS8765
}
