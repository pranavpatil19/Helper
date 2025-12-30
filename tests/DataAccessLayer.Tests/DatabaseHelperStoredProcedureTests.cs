using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;
using DataAccessLayer.Configuration;
using DataAccessLayer.Mapping;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Configuration;
using Xunit;
using FluentValidation;

namespace DataAccessLayer.Tests;

#nullable enable
#pragma warning disable CS8632
#pragma warning disable CS8765

public sealed class DatabaseHelperStoredProcedureTests
{
    private static readonly DatabaseOptions Options = new()
    {
        Provider = DatabaseProvider.SqlServer,
        ConnectionString = "Server=.;Database=Fake;Trusted_Connection=True;"
    };

    [Fact]
    public async Task ExecuteStoredProcedureAsync_UsesStoredProcedureCommandType()
    {
        var commandFactory = new CapturingCommandFactory();
        var helper = CreateHelper(commandFactory);

        await helper.ExecuteStoredProcedureAsync("dbo.DoWork");

        Assert.NotNull(commandFactory.LastRequest);
        Assert.Equal(CommandType.StoredProcedure, commandFactory.LastRequest!.CommandType);
        Assert.Equal("dbo.DoWork", commandFactory.LastRequest.CommandText);
    }

    [Fact]
    public async Task QueryStoredProcedureAsync_MapsRows()
    {
        var commandFactory = new CapturingCommandFactory
        {
            ReaderRows = new[]
            {
                new object[] { 42, "answer" }
            }
        };
        var helper = CreateHelper(commandFactory);

        var result = await helper.QueryStoredProcedureAsync(
            "dbo.GetValues",
            reader => (reader.GetInt32(0), reader.GetString(1)));

        Assert.Single(result.Data);
        Assert.Equal((42, "answer"), result.Data[0]);
    }

    [Fact]
    public async Task QueryAsync_WithRowMapper_ProjectsUsingReflection()
    {
        var commandFactory = new CapturingCommandFactory
        {
            ReaderRows =
            [
                new object[] { 7, "alpha" }
            ]
        };
        var helper = CreateHelper(commandFactory);
        var mapperRequest = new RowMapperRequest
        {
            PropertyToColumnMap = new Dictionary<string, string>
            {
                [nameof(TestEntity.Id)] = "Column0",
                [nameof(TestEntity.Name)] = "Column1"
            }
        };

        var result = await helper.QueryAsync<TestEntity>(
            new DbCommandRequest { CommandText = "SELECT Value" },
            mapperRequest: mapperRequest);

        Assert.Single(result.Data);
        Assert.Equal(7, result.Data[0].Id);
        Assert.Equal("alpha", result.Data[0].Name);
    }

    private static IDatabaseHelper CreateHelper(IDbCommandFactory commandFactory)
    {
        var connectionFactory = new FakeDbConnectionFactory();
        var scopeManager = new ConnectionScopeManager(connectionFactory, Options);
        var helperOptions = new DbHelperOptions();
        var telemetry = new DataAccessTelemetry(helperOptions);
        var rowMapperFactory = new RowMapperFactory(helperOptions);
        var runtimeOptions = new DalRuntimeOptions();
        return new DatabaseHelper(
            scopeManager,
            commandFactory,
            Options,
            CreateResilienceStrategy(),
            NullLogger<DatabaseHelper>.Instance,
            telemetry,
            runtimeOptions,
            Array.Empty<IValidator<DbCommandRequest>>(),
            rowMapperFactory);
    }

    [Fact]
    public async Task ExecuteReaderAsync_ReturnsLeaseAndDisposesResources()
    {
        var connectionFactory = new FakeDbConnectionFactory();
        var commandFactory = new CapturingCommandFactory
        {
            ReaderRows = new[]
            {
                new object[] { 1 },
                new object[] { 1 }
            }
        };
        var scopeManager = new ConnectionScopeManager(connectionFactory, Options);
        var helperOptions = new DbHelperOptions();
        var telemetry = new DataAccessTelemetry(helperOptions);
        var rowMapperFactory = new RowMapperFactory(helperOptions);
        var runtimeOptions = new DalRuntimeOptions();
        var helper = new DatabaseHelper(
            scopeManager,
            commandFactory,
            Options,
            CreateResilienceStrategy(),
            NullLogger<DatabaseHelper>.Instance,
            telemetry,
            runtimeOptions,
            Array.Empty<IValidator<DbCommandRequest>>(),
            rowMapperFactory);

        await using (var lease = await helper.ExecuteReaderAsync(new DbCommandRequest
        {
            CommandText = "SELECT 1",
            CommandType = CommandType.Text
        }))
        {
            Assert.NotNull(lease.Reader);
        }

        Assert.True(commandFactory.WasReturned);
        Assert.True(connectionFactory.LastConnection!.IsDisposed);
    }

    private sealed class FakeDbConnectionFactory : IDbConnectionFactory
    {
        public FakeDbConnection? LastConnection { get; private set; }

        public DbConnection CreateConnection(DatabaseOptions options)
        {
            LastConnection = new FakeDbConnection();
            return LastConnection;
        }
    }

    private sealed class CapturingCommandFactory : IDbCommandFactory
    {
        public DbCommandRequest? LastRequest { get; private set; }
        public object[][] ReaderRows { get; set; } = Array.Empty<object[]>();
        public FakeDbCommand? LastCommand { get; private set; }
        public bool WasReturned { get; private set; }

        public DbCommand GetCommand(DbConnection connection, DbCommandRequest request)
        {
            LastRequest = request;
            LastCommand = new FakeDbCommand(ReaderRows)
            {
                Connection = connection
            };
            return LastCommand;
        }

        public Task<DbCommand> GetCommandAsync(DbConnection connection, DbCommandRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetCommand(connection, request));
        }

        public void ReturnCommand(DbCommand command)
        {
            if (command is FakeDbCommand fake)
            {
                fake.Reset();
            }
            WasReturned = true;
        }
    }

    private sealed class FakeDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;
        public bool IsDisposed { get; private set; }

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
        protected override DbCommand CreateDbCommand() => new FakeDbCommand(Array.Empty<object[]>());

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FakeDbTransaction(FakeDbConnection connection) : DbTransaction
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
        private readonly object[][] _rows;

        public FakeDbCommand(object[][] rows)
        {
            _rows = rows;
        }

        public bool IsDisposed { get; private set; }

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        protected override DbConnection? DbConnection { get; set; } = null!;
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public override object ExecuteScalar() =>
            _rows.Length > 0 && _rows[0].Length > 0 ? _rows[0][0] ?? DBNull.Value : DBNull.Value;

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) =>
            Task.FromResult<object?>(ExecuteScalar());
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new FakeDbDataReader(_rows);
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
            => Task.FromResult<DbDataReader>(new FakeDbDataReader(_rows));

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }

        public void Reset()
        {
            Parameters.Clear();
            Transaction = null;
            Connection = null!;
            CommandText = string.Empty;
            CommandType = CommandType.Text;
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

    private sealed class FakeDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _inner = new();
        public override int Count => _inner.Count;
        public override object SyncRoot => this;
        public override int Add(object value)
        {
            _inner.Add((DbParameter)value);
            return _inner.Count - 1;
        }
        public override void AddRange(Array values)
        {
            foreach (DbParameter parameter in values)
            {
                _inner.Add(parameter);
            }
        }
        public override void Clear() => _inner.Clear();
        public override bool Contains(object value) => _inner.Contains((DbParameter)value);
        public override bool Contains(string value) => _inner.Exists(p => p.ParameterName == value);
        public override void CopyTo(Array array, int index) => _inner.ToArray().CopyTo(array, index);
        public override IEnumerator GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();
        public override int IndexOf(object value) => _inner.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _inner.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => _inner.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _inner.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _inner.RemoveAt(index);
        public override void RemoveAt(string parameterName)
        {
            var idx = IndexOf(parameterName);
            if (idx >= 0)
            {
                _inner.RemoveAt(idx);
            }
        }
        protected override DbParameter GetParameter(int index) => _inner[index];
        protected override DbParameter GetParameter(string parameterName) => _inner[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _inner[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var idx = IndexOf(parameterName);
            if (idx >= 0)
            {
                _inner[idx] = value;
            }
        }
    }

    private sealed class FakeDbDataReader : DbDataReader
    {
        private readonly object[][] _rows;
        private int _index = -1;

        public FakeDbDataReader(object[][] rows) =>
            _rows = rows.Length == 0 ? new[] { new object[] { 1 } } : rows;

        public override bool Read() => ++_index < _rows.Length;
        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());
        public override int FieldCount => _rows.Length == 0 ? 0 : _rows[0].Length;
        public override object GetValue(int ordinal) => _rows[_index][ordinal];
        public override bool HasRows => _rows.Length > 0;
        public override int RecordsAffected => 0;
        public override bool IsClosed => false;
        public override int Depth => 0;
        public override bool NextResult() => false;
        public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
        public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => (char)GetValue(ordinal);
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
        public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
        public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
        public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
        public override Type GetFieldType(int ordinal) => _rows.Length == 0 ? typeof(object) : _rows[0][ordinal].GetType();
        public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
        public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
        public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
        public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
        public override string GetName(int ordinal) => $"Column{ordinal}";
        public override int GetOrdinal(string name) => int.TryParse(name.Replace("Column", string.Empty), out var value) ? value : -1;
        public override string GetString(int ordinal) => (string)GetValue(ordinal);
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));
        public override bool IsDBNull(int ordinal) => GetValue(ordinal) is null or DBNull;
        public override IEnumerator GetEnumerator() => ((IEnumerable)_rows).GetEnumerator();
        public override int GetValues(object[] values)
        {
            if (_rows.Length == 0)
            {
                return 0;
            }

            var length = Math.Min(values.Length, FieldCount);
            for (var i = 0; i < length; i++)
            {
                values[i] = GetValue(i);
            }

            return length;
        }
    }

    private static IResilienceStrategy CreateResilienceStrategy() =>
        new ResilienceStrategy(
            new ResilienceOptions
            {
                EnableCommandRetries = false,
                EnableTransactionRetries = false
            },
            NullLogger<ResilienceStrategy>.Instance);

    private sealed class TestEntity
    {
        public int Id { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
    }
}
