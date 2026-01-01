using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using DataAccessLayer.Execution;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Telemetry;
using DataAccessLayer.Transactions;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests.Bulk;

public sealed class BulkWriteHelperTests
{
    [Fact]
    public async Task ExecuteAsync_OracleInsert_WritesAllRows()
    {
        var databaseOptions = new DatabaseOptions
        {
            Provider = DatabaseProvider.Oracle,
            ConnectionString = "Server=Fake;"
        };

        var connectionFactory = new FakeConnectionFactory();
        var connectionScopeManager = new ConnectionScopeManager(connectionFactory, databaseOptions);
        var telemetry = new NoOpTelemetry();
        var helper = new BulkWriteHelper(
            databaseOptions,
            new IBulkEngine[]
            {
                new OracleBulkEngine(connectionScopeManager, telemetry)
            });

        var mapping = new TestMapping();
        var operation = new BulkOperation<TestRow>(mapping);
        var rows = new List<TestRow>
        {
            new(1, "Alpha"),
            new(2, "Beta")
        };

        FakeCommand.Reset();
        var result = await helper.ExecuteAsync(operation, rows);

        Assert.Equal(2, result.RowsInserted);
        Assert.Equal(1, FakeCommand.ExecutionCount);
        Assert.Equal(rows.Count, FakeCommand.LastArrayBindCount);
    }

    [Fact]
    public async Task ExecuteAsync_UsesAmbientTransactionWhenAvailable()
    {
        var databaseOptions = new DatabaseOptions
        {
            Provider = DatabaseProvider.Oracle,
            ConnectionString = "Server=Fake;"
        };

        var connectionFactory = new FakeConnectionFactory();
        var connectionScopeManager = new ConnectionScopeManager(connectionFactory, databaseOptions);
        var telemetry = new NoOpTelemetry();
        var helper = new BulkWriteHelper(
            databaseOptions,
            new IBulkEngine[]
            {
                new OracleBulkEngine(connectionScopeManager, telemetry)
            });

        var mapping = new TestMapping();
        var operation = new BulkOperation<TestRow>(mapping);
        var rows = new List<TestRow>
        {
            new(5, "Txn Alpha"),
            new(6, "Txn Beta")
        };

        FakeCommand.Reset();
        var connection = new FakeConnection();
        await connection.OpenAsync();
        var transaction = new FakeDbTransaction(connection);
        var scope = new FakeTransactionScope(connection, transaction);

        using (TransactionScopeAmbient.Push(scope))
        {
            var result = await helper.ExecuteAsync(operation, rows);
            Assert.Equal(rows.Count, result.RowsInserted);
        }

        Assert.Equal(1, FakeCommand.ExecutionCount);
        Assert.Equal(rows.Count, FakeCommand.LastArrayBindCount);
    }

    #region Fakes

    private sealed record TestRow(int Id, string Name);

    private sealed class TestMapping : BulkMapping<TestRow>
    {
        public TestMapping()
            : base(
                "TestRows",
                new[]
                {
                    new BulkColumn("id"),
                    new BulkColumn("name")
                },
                row => new object?[] { row.Id, row.Name })
        {
        }
    }

    private sealed class FakeConnectionFactory : IDbConnectionFactory
    {
        public DbConnection CreateConnection(DatabaseOptions options) => new FakeConnection();
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
        protected override DbCommand CreateDbCommand() => new FakeCommand();
    }

    private sealed class FakeCommand : DbCommand
    {
        private readonly FakeParameterCollection _parameters = new();

        public static int ExecutionCount { get; private set; }
        public static int LastArrayBindCount { get; private set; }

        public static void Reset()
        {
            ExecutionCount = 0;
            LastArrayBindCount = 0;
        }

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.None;
        [System.Diagnostics.CodeAnalysis.AllowNull]
        protected override DbConnection? DbConnection { get; set; } = null!;
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public int ArrayBindCount { get; set; } = 0;

        public override void Cancel() { }
        public override int ExecuteNonQuery()
        {
            ExecutionCount++;
            LastArrayBindCount = ArrayBindCount;
            return ArrayBindCount;
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            ExecutionCount++;
            LastArrayBindCount = ArrayBindCount;
            return Task.FromResult(ArrayBindCount);
        }

        public override object ExecuteScalar() => new object();
        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult<object?>(null);
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }

    private sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
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

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _parameters = new();

        public override int Count => _parameters.Count;
        public override object SyncRoot => ((System.Collections.IList)_parameters).SyncRoot;
        public override int Add(object value)
        {
            _parameters.Add((DbParameter)value);
            return _parameters.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
            {
                Add(value!);
            }
        }

        public override void Clear() => _parameters.Clear();
        public override bool Contains(object value) => _parameters.Contains((DbParameter)value);
        public override bool Contains(string value) => _parameters.Exists(p => string.Equals(p.ParameterName, value, StringComparison.Ordinal));
        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_parameters).CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _parameters.GetEnumerator();
        public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _parameters.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.Ordinal));
        public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _parameters.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _parameters.RemoveAt(index);
        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            {
                _parameters.RemoveAt(index);
            }
        }

        protected override DbParameter GetParameter(int index) => _parameters[index];
        protected override DbParameter GetParameter(string parameterName) => _parameters[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            {
                _parameters[index] = value;
            }
            else
            {
                _parameters.Add(value);
            }
        }
    }

    private sealed class FakeTransactionScope : ITransactionScope
    {
        public FakeTransactionScope(DbConnection connection, DbTransaction transaction)
        {
            Connection = connection;
            Transaction = transaction;
        }

        public DbConnection Connection { get; }

        public DbTransaction? Transaction { get; }

        public Task BeginSavepointAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void BeginSavepoint(string name) { }

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Commit() { }

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void ReleaseSavepoint(string name) { }

        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Rollback() { }

        public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void RollbackToSavepoint(string name) { }
    }

    private sealed class FakeDbTransaction : DbTransaction
    {
        private readonly DbConnection _connection;

        public FakeDbTransaction(DbConnection connection) => _connection = connection;

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection DbConnection => _connection;

        public override void Commit() { }

        public override Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public override void Rollback() { }

        public override Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpTelemetry : IDataAccessTelemetry
    {
        public Activity? StartCommandActivity(string operation, DbCommandRequest request, DatabaseOptions defaultOptions) => null;
        public void RecordCommandResult(Activity? activity, DbExecutionResult execution, int? resultCount = null) { }
        public Activity? StartBulkActivity(DatabaseProvider provider, string destinationTable) => null;
        public void RecordBulkResult(Activity? activity, int rowsInserted) { }
        public string GetCommandDisplayName(DbCommandRequest request) => request.TraceName ?? "[redacted]";
    }

    #endregion
}
