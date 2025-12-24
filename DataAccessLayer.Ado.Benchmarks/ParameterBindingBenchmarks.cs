using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using BenchmarkDotNet.Attributes;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Execution;
using DataAccessLayer.Mapping;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Configuration;

namespace DataAccessLayer.Ado.Benchmarks;

[MemoryDiagnoser]
public class ParameterBindingBenchmarks
{
    private readonly DbCommandFactory _commandFactory;
    private readonly DbCommandRequest _request;

    public ParameterBindingBenchmarks()
    {
        var databaseOptions = new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=.;Database=Test;Integrated Security=true;"
        };

        var pool = new DbParameterPool(databaseOptions.CommandPool);
        var binder = new ParameterBinder(pool, databaseOptions.ParameterBinding, databaseOptions.InputNormalization);
        _commandFactory = new DbCommandFactory(
            binder,
            pool,
            databaseOptions,
            databaseOptions.CommandPool,
            NullLogger<DbCommandFactory>.Instance);

        _request = new DbCommandRequest
        {
            CommandText = "dbo.DoThing",
            CommandType = CommandType.StoredProcedure,
            Parameters = new[]
            {
                new DbParameterDefinition { Name = "Id", DbType = DbType.Int32, Value = 123 },
                new DbParameterDefinition { Name = "Name", DbType = DbType.String, Size = 128, Value = "bench" },
                new DbParameterDefinition { Name = "LastUpdatedUtc", DbType = DbType.DateTime2, Value = DateTime.UtcNow },
            }
        };
    }

    [Benchmark]
    public void BuildCommand()
    {
        using var connection = new NoOpConnection();
        var command = _commandFactory.Rent(connection, _request);
        _ = command.Parameters.Count;
        _commandFactory.Return(command);
    }

#pragma warning disable CS8765

    private sealed class NoOpConnection : DbConnection
    {
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new NoOpCommand(this);
    }

    private sealed class NoOpCommand(DbConnection connection) : DbCommand
    {
        private readonly DbParameterCollection _parameters = new FakeParameterCollection();
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        protected override DbConnection DbConnection { get; set; } = connection;
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        public override void Cancel() { }
        protected override DbParameter CreateDbParameter() => new FakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        public override int ExecuteNonQuery() => 1;
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
    }

    private sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
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
        private readonly List<DbParameter> _items = new();
        public override int Count => _items.Count;
        public override object SyncRoot => this;
        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }
        public override void AddRange(Array values)
        {
            foreach (var value in values)
            {
                Add(value!);
            }
        }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value)
        {
            return _items.Contains((DbParameter)value);
        }
        public override bool Contains(string value) => _items.Exists(p => p.ParameterName == value);
        public override void CopyTo(Array array, int index) => _items.ToArray().CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items.First(p => p.ParameterName == parameterName);
        public override int IndexOf(object value)
        {
            return _items.IndexOf((DbParameter)value);
        }
        public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value)
        {
            _items.Insert(index, (DbParameter)value);
        }
        public override void Remove(object value)
        {
            _items.Remove((DbParameter)value);
        }
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) => _items.RemoveAll(p => p.ParameterName == parameterName);
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var idx = IndexOf(parameterName);
            if (idx >= 0)
            {
                _items[idx] = value;
            }
        }
    }
#pragma warning restore CS8765
}
