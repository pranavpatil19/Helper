using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using DataAccessLayer.Execution;
using DataAccessLayer.Common.DbHelper;
using Microsoft.Data.SqlClient;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests.Providers.SqlServer;

#nullable enable

#pragma warning disable CS8765
#pragma warning disable CS8620

public sealed class SqlServerBulkWriterTests
{
    [Fact]
    public void Write_InvokesClientWithMappings()
    {
        var clientFactory = new FakeBulkCopyClientFactory();
        var options = new SqlServerBulkWriterOptions<Dummy>
        {
            DestinationTable = "dbo.Target",
            ColumnNames = new[] { "Id", "Name" },
            ValueSelector = item => new object?[] { item.Id, item.Name }
        };

        var writer = new SqlServerBulkWriter<Dummy>(() => new FakeSqlConnection(), options, clientFactory);
        writer.Write(new[] { new Dummy { Id = 1, Name = "A" } });

        Assert.Equal("dbo.Target", clientFactory.Client!.DestinationTableName);
        Assert.Equal(2, clientFactory.Client.ColumnMappings.Count);
    }

    [Fact]
    public async Task WriteAsync_UsesValueSelector()
    {
        var clientFactory = new FakeBulkCopyClientFactory();
        var options = new SqlServerBulkWriterOptions<Dummy>
        {
            DestinationTable = "dbo.Target",
            ColumnNames = new[] { "Id" },
            ValueSelector = item => new object?[] { item.Id }
        };

        var writer = new SqlServerBulkWriter<Dummy>(() => new FakeSqlConnection(), options, clientFactory);
        await writer.WriteAsync(new[] { new Dummy { Id = 5 } });

        Assert.Single(clientFactory.Client!.Rows);
        Assert.Equal(5, clientFactory.Client.Rows[0][0]);
    }

    private sealed class Dummy
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class FakeSqlConnection : DbConnection
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
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new FakeDbTransaction(this);
        protected override DbCommand CreateDbCommand() => new FakeDbCommand();
    }

    private sealed class FakeDbTransaction(FakeSqlConnection connection) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => connection;
        public override void Commit() { }
        public override Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override void Rollback() { }
        public override Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDbCommand : DbCommand
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        protected override DbConnection? DbConnection { get; set; } = null!;
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object ExecuteScalar() => new object();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new FakeDbDataReader();
    }

    private sealed class FakeDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ParameterName { get; set; } = string.Empty;
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override object Value { get; set; } = null!;
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class FakeDbParameterCollection : DbParameterCollection
    {
        public override int Count => 0;
        public override object SyncRoot => this;
        public override int Add(object value) => 0;
        public override void AddRange(Array values) { }
        public override void Clear() { }
        public override bool Contains(object value) => false;
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index) { }
        public override IEnumerator GetEnumerator() => ((IEnumerable)Array.Empty<object>()).GetEnumerator();
        public override int IndexOf(object value) => -1;
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, object value) { }
        public override void Remove(object value) { }
        public override void RemoveAt(int index) { }
        public override void RemoveAt(string parameterName) { }
        protected override DbParameter GetParameter(int index) => new FakeDbParameter();
        protected override DbParameter GetParameter(string parameterName) => new FakeDbParameter();
        protected override void SetParameter(int index, DbParameter value) { }
        protected override void SetParameter(string parameterName, DbParameter value) { }
    }

    private sealed class FakeDbDataReader : DbDataReader
    {
        public override bool Read() => false;
        public override int FieldCount => 0;
        public override object this[int ordinal] => null!;
        public override object this[string name] => null!;
        public override int Depth => 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override bool HasRows => false;
        public override bool GetBoolean(int ordinal) => false;
        public override byte GetByte(int ordinal) => 0;
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => '\0';
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override string GetDataTypeName(int ordinal) => string.Empty;
        public override DateTime GetDateTime(int ordinal) => DateTime.MinValue;
        public override decimal GetDecimal(int ordinal) => 0;
        public override double GetDouble(int ordinal) => 0;
        public override Type GetFieldType(int ordinal) => typeof(object);
        public override float GetFloat(int ordinal) => 0;
        public override Guid GetGuid(int ordinal) => Guid.Empty;
        public override short GetInt16(int ordinal) => 0;
        public override int GetInt32(int ordinal) => 0;
        public override long GetInt64(int ordinal) => 0;
        public override string GetName(int ordinal) => string.Empty;
        public override int GetOrdinal(string name) => -1;
        public override string GetString(int ordinal) => string.Empty;
        public override object GetValue(int ordinal) => null!;
        public override int GetValues(object[] values) => 0;
        public override bool IsDBNull(int ordinal) => true;
        public override bool NextResult() => false;
        public override IEnumerator GetEnumerator() => ((IEnumerable)Array.Empty<object>()).GetEnumerator();
    }

    private sealed class FakeBulkCopyClientFactory : ISqlBulkCopyClientFactory
    {
        public FakeClient? Client { get; private set; }

        public ISqlBulkCopyClient Create(DbConnection connection, SqlBulkCopyOptions options, DbTransaction? transaction = null)
        {
            Client = new FakeClient();
            return Client;
        }
    }

    private sealed class FakeClient : ISqlBulkCopyClient
    {
        public List<string> ColumnMappings { get; } = new();
        public List<object?[]> Rows { get; } = new();
        public string DestinationTableName { get; set; } = string.Empty;
        public int BatchSize { get; set; }
        public int BulkCopyTimeout { get; set; }

        public void AddColumnMapping(string sourceColumn, string destinationColumn) => ColumnMappings.Add(sourceColumn);

        public void WriteToServer(IDataReader reader)
        {
            while (reader.Read())
            {
                var values = new object?[reader.FieldCount];
                reader.GetValues(values);
                Rows.Add(values);
            }
        }

        public Task WriteToServerAsync(IDataReader reader, CancellationToken cancellationToken = default)
        {
            WriteToServer(reader);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }
}

#pragma warning restore CS8620
#pragma warning restore CS8765
