using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using Xunit;

namespace DataAccessLayer.Tests.Providers.Postgres;

#nullable enable

#pragma warning disable CS8765

public sealed class PostgresBulkWriterTests
{
    [Fact]
    public void Write_SendsRowsToClient()
    {
        var clientFactory = new FakeCopyClientFactory();
        var options = new PostgresBulkWriterOptions<Dummy>
        {
            DestinationTable = "public.items",
            ColumnNames = new[] { "id", "name" },
            ValueSelector = d => new object?[] { d.Id, d.Name }
        };

        var writer = new PostgresBulkWriter<Dummy>(
            () => new FakeConnection(),
            options,
            clientFactory);

        writer.Write(new[] { new Dummy { Id = 42, Name = "hello" } });

        Assert.Single(clientFactory.Client!.Rows);
        Assert.Equal("hello", clientFactory.Client.Rows[0][1]);
    }

    [Fact]
    public async Task WriteAsync_UsesAsyncApi()
    {
        var clientFactory = new FakeCopyClientFactory();
        var options = new PostgresBulkWriterOptions<Dummy>
        {
            CopyCommand = "COPY public.items (id) FROM STDIN (FORMAT BINARY)",
            ColumnNames = new[] { "id" },
            ValueSelector = d => new object?[] { d.Id }
        };

        var writer = new PostgresBulkWriter<Dummy>(
            () => new FakeConnection(),
            options,
            clientFactory);

        await writer.WriteAsync(new[] { new Dummy { Id = 5 } });

        Assert.Equal(5, clientFactory.Client!.Rows[0][0]);
        Assert.True(clientFactory.Client.CompletedAsync);
    }

    private sealed class Dummy
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class FakeConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

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
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    private sealed class FakeCopyClientFactory : IPostgresCopyClientFactory
    {
        public FakeCopyClient? Client { get; private set; }

        public IPostgresCopyClient Create(DbConnection connection, string copyCommand)
        {
            Client = new FakeCopyClient(copyCommand);
            return Client;
        }
    }

    private sealed class FakeCopyClient : IPostgresCopyClient
    {
        public FakeCopyClient(string command) => Command = command;
        public string Command { get; }
        public List<object?[]> Rows { get; } = new();
        public bool CompletedAsync { get; private set; }
        public IReadOnlyList<BulkColumn>? Columns { get; private set; }

        public void ConfigureColumns(IReadOnlyList<BulkColumn>? columns) => Columns = columns;

        public void WriteRow(object?[] values) => Rows.Add(values);

        public Task WriteRowAsync(object?[] values, CancellationToken cancellationToken = default)
        {
            Rows.Add(values);
            return Task.CompletedTask;
        }

        public void Complete() { }

        public Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            CompletedAsync = true;
            return Task.CompletedTask;
        }

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

#pragma warning restore CS8765
