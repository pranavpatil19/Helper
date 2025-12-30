using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using BenchmarkDotNet.Attributes;
using DataAccessLayer.Execution;
using DataAccessLayer.Common.DbHelper;
using Shared.Configuration;

namespace Data.Benchmarks;

[MemoryDiagnoser]
/// <summary>
/// Benchmarks the pooled command get/return cycle to catch regressions in command/parameter pooling.
/// </summary>
public class DbCommandFactoryBenchmarks
{
    private readonly DbCommandFactory _factory;
    private readonly DbCommandRequest _request;
    private readonly FakeConnection _connection;

    public DbCommandFactoryBenchmarks()
    {
        var commandPool = new CommandPoolOptions
        {
            EnableCommandPooling = true,
            EnableParameterPooling = true,
            MaximumRetainedCommands = 256,
            MaximumRetainedParameters = 1024
        };

        var defaults = new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=(local);Database=Benchmark;Trusted_Connection=True;",
            CommandPool = commandPool
        };

        var parameterPool = new DbParameterPool(commandPool);
        var binder = new ParameterBinder(parameterPool);
        _factory = new DbCommandFactory(binder, parameterPool, defaults, commandPool);
        _request = new DbCommandRequest { CommandText = "SELECT 1" };
        _connection = new FakeConnection();
        _connection.Open();
    }

    /// <summary>
    /// Measures the cost of getting and returning a pooled command.
    /// </summary>
    [Benchmark]
    public void GetAndReturn()
    {
        var command = _factory.GetCommand(_connection, _request);
        _factory.ReturnCommand(command);
    }

    /// <summary>
    /// Releases owned resources once benchmarks complete.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _connection.Dispose();
    }

#pragma warning disable CS8765

    private sealed class FakeConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new FakeTransaction(this);
        protected override DbCommand CreateDbCommand() => new FakeCommand { Connection = this };
    }

    private sealed class FakeTransaction(FakeConnection connection) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => connection;
        public override void Commit() { }
        public override void Rollback() { }
    }

    private sealed class FakeCommand : DbCommand
    {
        private readonly FakeParameterCollection _parameters = new();

        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        protected override DbConnection DbConnection { get; set; } = null!;
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 1;
        public override object? ExecuteScalar() => 1;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new FakeReader();
    }

    private sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; } = string.Empty;
        public override string SourceColumn { get; set; } = string.Empty;
        public override object Value { get; set; } = DBNull.Value;
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _parameters = new();
        public override int Count => _parameters.Count;
        public override object SyncRoot => this;
        public override int Add(object value)
        {
            _parameters.Add((DbParameter)value);
            return _parameters.Count - 1;
        }
        public override void AddRange(Array values)
        {
            foreach (DbParameter parameter in values)
            {
                _parameters.Add(parameter);
            }
        }
        public override void Clear() => _parameters.Clear();
        public override bool Contains(object value)
        {
            return _parameters.Contains((DbParameter)value);
        }
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index) => _parameters.ToArray().CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();
        public override int IndexOf(object value)
        {
            return _parameters.IndexOf((DbParameter)value);
        }
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, object value)
        {
            _parameters.Insert(index, (DbParameter)value);
        }
        public override void Remove(object value)
        {
            _parameters.Remove((DbParameter)value);
        }
        public override void RemoveAt(int index) => _parameters.RemoveAt(index);
        public override void RemoveAt(string parameterName) { }
        protected override DbParameter GetParameter(int index) => _parameters[index];
        protected override DbParameter GetParameter(string parameterName) => throw new NotSupportedException();
        protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => throw new NotSupportedException();
    }

    private sealed class FakeReader : DbDataReader
    {
        public override bool Read() => false;
        public override int FieldCount => 0;
        public override object this[int ordinal] => null!;
        public override object this[string name] => null!;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override bool NextResult() => false;
        public override int Depth => 0;
        public override IEnumerator GetEnumerator() => Array.Empty<object?>().GetEnumerator();
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
    }
#pragma warning restore CS8765
}
