using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Transactions;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests.Transactions;

public sealed class SavepointManagerTests
{
    [Fact]
    public void SqlServer_BeginRollback_NoReleaseCommand()
    {
        var connection = new RecordingConnection();
        var transaction = new RecordingTransaction(connection);
        var manager = CreateManager();
        var options = new DatabaseOptions { Provider = DatabaseProvider.SqlServer };

        manager.BeginSavepoint(transaction, "stage1", options);
        manager.RollbackToSavepoint(transaction, "stage1", options);
        manager.ReleaseSavepoint(transaction, "stage1", options);

        Assert.Equal(new[]
        {
            "SAVE TRANSACTION stage1",
            "ROLLBACK TRANSACTION stage1"
        }, connection.ExecutedCommands);
    }

    [Fact]
    public void Postgres_IncludesReleaseCommand()
    {
        var connection = new RecordingConnection();
        var transaction = new RecordingTransaction(connection);
        var manager = CreateManager();
        var options = new DatabaseOptions { Provider = DatabaseProvider.PostgreSql };

        manager.BeginSavepoint(transaction, "batch_1", options);
        manager.RollbackToSavepoint(transaction, "batch_1", options);
        manager.ReleaseSavepoint(transaction, "batch_1", options);

        Assert.Equal(new[]
        {
            "SAVEPOINT batch_1",
            "ROLLBACK TO SAVEPOINT batch_1",
            "RELEASE SAVEPOINT batch_1"
        }, connection.ExecutedCommands);
    }

    [Fact]
    public void Oracle_ReleaseIsNoOp()
    {
        var connection = new RecordingConnection();
        var transaction = new RecordingTransaction(connection);
        var manager = CreateManager();
        var options = new DatabaseOptions { Provider = DatabaseProvider.Oracle };

        manager.BeginSavepoint(transaction, "step-1", options);
        manager.ReleaseSavepoint(transaction, "step-1", options);

        Assert.Equal(new[]
        {
            "SAVEPOINT step-1"
        }, connection.ExecutedCommands);
    }

    [Fact]
    public void BeginSavepoint_RejectsInvalidName()
    {
        var connection = new RecordingConnection();
        var transaction = new RecordingTransaction(connection);
        var manager = CreateManager();
        var options = new DatabaseOptions { Provider = DatabaseProvider.PostgreSql };

        Assert.Throws<System.ArgumentException>(() => manager.BeginSavepoint(transaction, "bad name", options));
    }

    private static SavepointManager CreateManager() =>
        new SavepointManager(NullLogger<SavepointManager>.Instance);

    private sealed class RecordingConnection : DbConnection
    {
        public List<string> ExecutedCommands { get; } = new();
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Recording";
        public override string DataSource => "Recording";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            new RecordingTransaction(this);

        protected override DbCommand CreateDbCommand() => new RecordingCommand(this, ExecutedCommands);
    }

    private sealed class RecordingTransaction(RecordingConnection connection) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => connection;
        public override void Commit() { }
        public override void Rollback() { }
    }

    private sealed class RecordingCommand : DbCommand
    {
        private readonly RecordingConnection _connection;
        private readonly List<string> _sink;

        public RecordingCommand(RecordingConnection connection, List<string> sink)
        {
            _connection = connection;
            _sink = sink;
        }

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        [AllowNull]
        protected override DbConnection? DbConnection { get => _connection; set => _ = value; }
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new RecordingParameterCollection();

        public override void Cancel() { }
        public override int ExecuteNonQuery()
        {
            _sink.Add(CommandText);
            return 0;
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            _sink.Add(CommandText);
            return Task.FromResult(0);
        }

        public override object ExecuteScalar() => new object();
        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult<object?>(null);
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new RecordingParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new System.NotImplementedException();
    }

    private sealed class RecordingParameterCollection : DbParameterCollection
    {
        public override int Count => 0;
        public override object SyncRoot => this;
        public override int Add(object value) => 0;
        public override void AddRange(Array values) { }
        public override void Clear() { }
        public override bool Contains(object value) => false;
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index) { }
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
        public override int IndexOf(object value) => -1;
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, object value) { }
        public override void Remove(object value) { }
        public override void RemoveAt(int index) { }
        public override void RemoveAt(string parameterName) { }
        protected override DbParameter GetParameter(int index) => throw new NotImplementedException();
        protected override DbParameter GetParameter(string parameterName) => throw new NotImplementedException();
        protected override void SetParameter(int index, DbParameter value) { }
        protected override void SetParameter(string parameterName, DbParameter value) { }
    }

    private sealed class RecordingParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [AllowNull]
        public override string ParameterName { get; set; } = string.Empty;
        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override object Value { get; set; } = null!;
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }
}
