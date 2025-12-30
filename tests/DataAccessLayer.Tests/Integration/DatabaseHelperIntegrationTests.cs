#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Configuration;
using DataAccessLayer.Mapping;
using DataAccessLayer.Telemetry;
using FluentValidation;
using DataAccessLayer.Transactions;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests.Integration;

public sealed class DatabaseHelperIntegrationTests
{
    [Theory]
    [InlineData(DatabaseProvider.SqlServer)]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.Oracle)]
    public async Task LoadDataSetAsync_ReadsMultipleResultSets(DatabaseProvider provider)
    {
        var command = FakeCommand.ForReader(new MultiResultReader(
            new[]
            {
                new TableData(new[] { "Id" }, new[] { new object?[] { 1 }, new object?[] { 2 } }),
                new TableData(new[] { "Name" }, new[] { new object?[] { "A" }, new object?[] { "B" } })
            }));
        var helper = CreateHelper(command, provider);

        var result = await helper.LoadDataSetAsync(new DbCommandRequest { CommandText = "multi" });

        Assert.Equal(2, result.Data.Tables.Count);
        Assert.Equal(2, result.Data.Tables[0].Rows.Count);
        Assert.Equal("B", result.Data.Tables[1].Rows[1][0]);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSqlOutputParameters()
    {
        var command = FakeCommand.ForNonQuery(setOutputs: parameters =>
        {
            foreach (FakeDbParameter parameter in parameters)
            {
                if (parameter.Direction != ParameterDirection.Input)
                {
                    parameter.Value = "UPDATED";
                }
            }
        });
        var helper = CreateHelper(command);
        var request = new DbCommandRequest
        {
            CommandText = "dbo.proc",
            Parameters =
            [
                DbParameterCollectionBuilder.Input("Id", 1),
                DbParameterCollectionBuilder.Output("Status", DbType.String)
            ]
        };

        var result = await helper.ExecuteAsync(request);

        Assert.Equal("UPDATED", result.OutputParameters["Status"]);
    }

    [Fact]
    public async Task ExecuteAsync_PostgresOutParametersAreEmulated()
    {
        var command = FakeCommand.ForReader(new MultiResultReader(
            new[]
            {
                new TableData(
                    new[] { "p_status", "p_reference" },
                    new[] { new object?[] { "OK", "ABC123" } })
            }));
        var helper = CreateHelper(command, provider: DatabaseProvider.PostgreSql);

        var request = new DbCommandRequest
        {
            CommandText = "public.transfer_fn",
            CommandType = CommandType.StoredProcedure,
            Parameters =
            [
                DbParameterCollectionBuilder.Input("p_id", 1),
                DbParameterCollectionBuilder.Output("p_status", DbType.String),
                DbParameterCollectionBuilder.Output("p_reference", DbType.String)
            ]
        };

        var result = await helper.ExecuteAsync(request);

        Assert.Equal("OK", result.OutputParameters["p_status"]);
        Assert.Equal("ABC123", result.OutputParameters["p_reference"]);
    }

    [Theory]
    [InlineData(DatabaseProvider.SqlServer)]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.Oracle)]
    public void LoadDataSet_ReadsMultipleResultSets(DatabaseProvider provider)
    {
        var command = FakeCommand.ForReader(new MultiResultReader(
            new[]
            {
                new TableData(new[] { "Id" }, new[] { new object?[] { 1 }, new object?[] { 2 } }),
                new TableData(new[] { "Name" }, new[] { new object?[] { "A" }, new object?[] { "B" } })
            }));
        var helper = CreateHelper(command, provider);

        var result = helper.LoadDataSet(new DbCommandRequest { CommandText = "multi-sync" });

        Assert.Equal(2, result.Data.Tables.Count);
        Assert.Equal("B", result.Data.Tables[1].Rows[1][0]);
    }

    [Theory]
    [InlineData(DatabaseProvider.SqlServer)]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.Oracle)]
    public async Task LoadDataTableAsync_ReadsRows(DatabaseProvider provider)
    {
        var command = FakeCommand.ForReader(new MultiResultReader(
            new[]
            {
                new TableData(
                    new[] { "Id", "Name" },
                    new[] { new object?[] { 1, "Row" } })
            }));
        var helper = CreateHelper(command, provider);

        var result = await helper.LoadDataTableAsync(new DbCommandRequest { CommandText = "single" });

        Assert.Single(result.Data.Rows);
        Assert.Equal("Row", result.Data.Rows[0][1]);
    }

    [Theory]
    [InlineData(DatabaseProvider.SqlServer)]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.Oracle)]
    public void LoadDataTable_ReadsRows(DatabaseProvider provider)
    {
        var command = FakeCommand.ForReader(new MultiResultReader(
            new[]
            {
                new TableData(
                    new[] { "Id", "Name" },
                    new[] { new object?[] { 1, "Row" } })
            }));
        var helper = CreateHelper(command, provider);

        var result = helper.LoadDataTable(new DbCommandRequest { CommandText = "single-sync" });

        Assert.Single(result.Data.Rows);
        Assert.Equal(1, result.Data.Rows[0][0]);
        Assert.Equal("Row", result.Data.Rows[0][1]);
    }

    [Fact]
    public async Task ExecuteAsync_ReusesExplicitTransactionAcrossMultipleTables()
    {
        var command = FakeCommand.ForNonQuery();
        var helper = CreateHelper(command);
        var connection = new FakeConnection(command);
        await connection.OpenAsync();
        var transaction = new RecordingTransaction(connection);

        var request1 = new DbCommandRequest
        {
            CommandText = "INSERT INTO Table1 ...",
            Connection = connection,
            Transaction = transaction
        };

        var request2 = new DbCommandRequest
        {
            CommandText = "INSERT INTO Table2 ...",
            Connection = connection,
            Transaction = transaction
        };

        await helper.ExecuteAsync(request1);
        await helper.ExecuteAsync(request2);

        Assert.Equal(2, command.NonQueryCount);
        Assert.All(command.Transactions, t => Assert.Same(transaction, t));
        Assert.Contains("INSERT INTO Table1 ...", command.CommandTexts);
        Assert.Contains("INSERT INTO Table2 ...", command.CommandTexts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInsertFails_AllowsCallerToRollback()
    {
        var command = FakeCommand.ForFailingNonQuery(new InvalidOperationException("boom"));
        var helper = CreateHelper(command);
        var connection = new FakeConnection(command);
        await connection.OpenAsync();
        var transaction = new RecordingTransaction(connection);

        var request = new DbCommandRequest
        {
            CommandText = "INSERT INTO BrokenTable ...",
            Connection = connection,
            Transaction = transaction
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => helper.ExecuteAsync(request));

        await transaction.RollbackAsync();

        Assert.True(transaction.RolledBack);
        Assert.False(transaction.Committed);
    }

    private static DatabaseHelper CreateHelper(FakeCommand command, DatabaseProvider provider = DatabaseProvider.SqlServer)
    {
        var options = new DatabaseOptions
        {
            Provider = provider,
            ConnectionString = "Server=.;Database=Fake;",
            WrapProviderExceptions = false
        };

        var connectionFactory = new StubConnectionFactory(command);
        var scopeManager = new ConnectionScopeManager(connectionFactory, options);
        var helperOptions = new DbHelperOptions();
        var telemetry = new DataAccessTelemetry(helperOptions);
        var rowMapperFactory = new RowMapperFactory(helperOptions);
        var runtimeOptions = new DalRuntimeOptions();
        return new DatabaseHelper(
            scopeManager,
            new StubCommandFactory(command),
            options,
            new ResilienceStrategy(options.Resilience, NullLogger<ResilienceStrategy>.Instance),
            NullLogger<DatabaseHelper>.Instance,
            telemetry,
            runtimeOptions,
            Array.Empty<IValidator<DbCommandRequest>>(),
            rowMapperFactory);
    }

    private sealed class StubConnectionFactory : IDbConnectionFactory
    {
        private readonly FakeConnection _connection;
        public StubConnectionFactory(FakeCommand command) => _connection = new FakeConnection(command);
        public DbConnection CreateConnection(DatabaseOptions options) => _connection;
    }

    private sealed class StubCommandFactory : IDbCommandFactory
    {
        private readonly FakeCommand _command;
        public StubCommandFactory(FakeCommand command) => _command = command;
        public DbCommand GetCommand(DbConnection connection, DbCommandRequest request)
        {
            _command.Request = request;
            _command.BindParameters(request.Parameters);
            _command.CommandText = request.CommandText;
            _command.CommandType = request.CommandType;
            return _command;
        }

        public Task<DbCommand> GetCommandAsync(DbConnection connection, DbCommandRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetCommand(connection, request));
        public void ReturnCommand(DbCommand command) { }
    }

    private sealed class FakeConnection : DbConnection
    {
        private readonly FakeCommand _command;
        private ConnectionState _state = ConnectionState.Closed;

        public FakeConnection(FakeCommand command) => _command = command;
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
        protected override DbCommand CreateDbCommand() => _command;
    }

    private sealed class RecordingTransaction : DbTransaction
    {
        private readonly DbConnection _connection;
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }

        public RecordingTransaction(DbConnection connection) => _connection = connection;

        public override void Commit() => Committed = true;
        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Commit();
            return Task.CompletedTask;
        }

        public override void Rollback() => RolledBack = true;
        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollback();
            return Task.CompletedTask;
        }

        protected override DbConnection DbConnection => _connection;
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    }

    private sealed class FakeCommand : DbCommand
    {
        private Func<int>? _nonQuery;
        private Func<CommandBehavior, DbDataReader>? _reader;
        private Func<CommandBehavior, CancellationToken, Task<DbDataReader>>? _readerAsync;

        public DbCommandRequest? Request { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override UpdateRowSource UpdatedRowSource { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        protected override DbConnection? DbConnection { get; set; } = null!;
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }

        public int NonQueryCount { get; private set; }
        public List<DbTransaction?> Transactions { get; } = new();
        public List<string> CommandTexts { get; } = new();

        public static FakeCommand ForReader(DbDataReader reader)
        {
            var command = new FakeCommand();
            command._reader = _ => reader;
            command._readerAsync = (_, __) => Task.FromResult(reader);
            return command;
        }

        public static FakeCommand ForNonQuery(Action<IReadOnlyList<FakeDbParameter>> setOutputs)
        {
            var command = new FakeCommand();
            command._nonQuery = () =>
            {
                setOutputs(((FakeParameterCollection)command.DbParameterCollection).Parameters);
                return 1;
            };
            return command;
        }

        public static FakeCommand ForNonQuery()
        {
            var command = new FakeCommand
            {
                _nonQuery = () => 1
            };
            return command;
        }

        public static FakeCommand ForFailingNonQuery(Exception exception)
        {
            var command = new FakeCommand
            {
                _nonQuery = () => throw exception
            };
            return command;
        }

        public override void Cancel() { }
        public override int ExecuteNonQuery()
        {
            NonQueryCount++;
            CommandTexts.Add(CommandText);
            Transactions.Add(DbTransaction);
            return _nonQuery?.Invoke() ?? 0;
        }
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(ExecuteNonQuery());
        public override object ExecuteScalar() => ExecuteNonQuery();
        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult<object?>(ExecuteScalar());
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => _reader?.Invoke(behavior) ?? new EmptyReader();
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
            _readerAsync?.Invoke(behavior, cancellationToken) ?? Task.FromResult<DbDataReader>(new EmptyReader());
        protected override DbParameter CreateDbParameter() => new FakeDbParameter();
        public override void Prepare() { }

        public void BindParameters(IReadOnlyList<DbParameterDefinition> definitions)
        {
            var collection = (FakeParameterCollection)DbParameterCollection;
            collection.Clear();
            foreach (var definition in definitions)
            {
                collection.Add(new FakeDbParameter
                {
                    ParameterName = definition.Name,
                    Direction = definition.Direction,
                    Value = definition.Value
                });
            }
        }
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

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        public List<FakeDbParameter> Parameters { get; } = new();
        public override int Count => Parameters.Count;
        public override object SyncRoot => this;
        public override int Add(object value)
        {
            Parameters.Add((FakeDbParameter)value);
            return Parameters.Count - 1;
        }
        public override void AddRange(Array values)
        {
            foreach (FakeDbParameter parameter in values)
            {
                Parameters.Add(parameter);
            }
        }
        public override void Clear() => Parameters.Clear();
        public override bool Contains(object value) => Parameters.Contains((FakeDbParameter)value);
        public override bool Contains(string value) => Parameters.Exists(p => p.ParameterName == value);
        public override void CopyTo(Array array, int index) => Parameters.ToArray().CopyTo(array, index);
        public override IEnumerator GetEnumerator() => Parameters.GetEnumerator();
        public override int IndexOf(object value) => Parameters.IndexOf((FakeDbParameter)value);
        public override int IndexOf(string parameterName) => Parameters.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => Parameters.Insert(index, (FakeDbParameter)value);
        public override void Remove(object value) => Parameters.Remove((FakeDbParameter)value);
        public override void RemoveAt(int index) => Parameters.RemoveAt(index);
        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            {
                Parameters.RemoveAt(index);
            }
        }
        protected override DbParameter GetParameter(int index) => Parameters[index];
        protected override DbParameter GetParameter(string parameterName) => Parameters[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => Parameters[index] = (FakeDbParameter)value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            {
                Parameters[index] = (FakeDbParameter)value;
            }
        }
    }

    private sealed class TableData
    {
        public TableData(string[] columns, object?[][] rows)
        {
            Columns = columns;
            Rows = rows;
        }

        public string[] Columns { get; }
        public object?[][] Rows { get; }
    }

    private sealed class MultiResultReader : DbDataReader
    {
        private readonly TableData[] _tables;
        private int _tableIndex;
        private int _rowIndex = -1;

        public MultiResultReader(TableData[] tables)
        {
            _tables = tables;
        }

        public override bool Read()
        {
            if (_rowIndex + 1 >= CurrentTable.Rows.Length)
            {
                return false;
            }

            _rowIndex++;
            return true;
        }

        public override bool NextResult()
        {
            if (_tableIndex + 1 >= _tables.Length)
            {
                return false;
            }

            _tableIndex++;
            _rowIndex = -1;
            return true;
        }

        private TableData CurrentTable => _tables[_tableIndex];

        public override int FieldCount => CurrentTable.Columns.Length;
        public override string GetName(int ordinal) => CurrentTable.Columns[ordinal];
        public override int GetOrdinal(string name) => Array.IndexOf(CurrentTable.Columns, name);
        public override object GetValue(int ordinal) => CurrentTable.Rows[_rowIndex][ordinal]!;
        public override bool IsDBNull(int ordinal) => CurrentTable.Rows[_rowIndex][ordinal] is null;
        public override string GetDataTypeName(int ordinal) => typeof(object).Name;
        public override Type GetFieldType(int ordinal) => typeof(object);
        public override bool HasRows => CurrentTable.Rows.Length > 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => -1;
        public override int Depth => 0;
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));
        public override IEnumerator GetEnumerator() => ((IEnumerable)CurrentTable.Rows[_rowIndex]).GetEnumerator();
        public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
        public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => (char)GetValue(ordinal);
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
        public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
        public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
        public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
        public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
        public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
        public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal)) ?? string.Empty;
        public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
        public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
        public override int GetValues(object[] values)
        {
            var row = CurrentTable.Rows[_rowIndex];
            var count = Math.Min(values.Length, row.Length);
            Array.Copy(row, values, count);
            return count;
        }

        public override DataTable GetSchemaTable()
        {
            var schema = new DataTable();
            schema.Columns.Add("ColumnName", typeof(string));
            schema.Columns.Add("ColumnOrdinal", typeof(int));
            schema.Columns.Add("DataType", typeof(Type));

            for (var i = 0; i < FieldCount; i++)
            {
                var row = schema.NewRow();
                row["ColumnName"] = GetName(i);
                row["ColumnOrdinal"] = i;
                row["DataType"] = GetFieldType(i);
                schema.Rows.Add(row);
            }

            return schema;
        }
    }

    private sealed class EmptyReader : DbDataReader
    {
        public override bool Read() => false;
        public override int FieldCount => 0;
        public override string GetName(int ordinal) => string.Empty;
        public override int GetOrdinal(string name) => -1;
        public override object GetValue(int ordinal) => throw new IndexOutOfRangeException();
        public override bool IsDBNull(int ordinal) => true;
        public override string GetDataTypeName(int ordinal) => string.Empty;
        public override Type GetFieldType(int ordinal) => typeof(object);
        public override bool NextResult() => false;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;
        public override object this[int ordinal] => throw new IndexOutOfRangeException();
        public override object this[string name] => throw new IndexOutOfRangeException();
        public override IEnumerator GetEnumerator() => Array.Empty<object?>().GetEnumerator();
        public override bool GetBoolean(int ordinal) => throw new IndexOutOfRangeException();
        public override byte GetByte(int ordinal) => throw new IndexOutOfRangeException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => throw new IndexOutOfRangeException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override Guid GetGuid(int ordinal) => throw new IndexOutOfRangeException();
        public override short GetInt16(int ordinal) => throw new IndexOutOfRangeException();
        public override int GetInt32(int ordinal) => throw new IndexOutOfRangeException();
        public override long GetInt64(int ordinal) => throw new IndexOutOfRangeException();
        public override float GetFloat(int ordinal) => throw new IndexOutOfRangeException();
        public override double GetDouble(int ordinal) => throw new IndexOutOfRangeException();
        public override string GetString(int ordinal) => throw new IndexOutOfRangeException();
        public override decimal GetDecimal(int ordinal) => throw new IndexOutOfRangeException();
        public override DateTime GetDateTime(int ordinal) => throw new IndexOutOfRangeException();
        public override int GetValues(object[] values) => 0;
    }
}
