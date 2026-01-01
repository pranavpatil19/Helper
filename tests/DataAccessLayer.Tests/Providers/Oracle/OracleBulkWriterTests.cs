using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using Xunit;

namespace DataAccessLayer.Tests.Providers.Oracle;

#nullable enable

#pragma warning disable CS8765

public sealed class OracleBulkWriterTests
{
    [Fact]
    public void Write_BindsArrayParameters()
    {
        FakeConnection.Reset();
        var options = CreateOptions();
        var writer = new OracleBulkWriter<Dummy>(() => new FakeConnection(), options);

        writer.Write(new[]
        {
            new Dummy { Id = 1, Name = "one" },
            new Dummy { Id = 2, Name = "two" }
        });

        var command = FakeConnection.LastCommand!;
        Assert.Equal(2, command.ArrayBindCount);
        Assert.Equal("p_id", command.Parameters[0].ParameterName);
        Assert.Equal(new object?[] { 1, 2 }, command.Parameters[0].Value);
    }

    [Fact]
    public async Task WriteAsync_UsesBatches()
    {
        FakeConnection.Reset();
        var options = CreateOptions();
        options.BatchSize = 1;

        var writer = new OracleBulkWriter<Dummy>(() => new FakeConnection(), options);
        await writer.WriteAsync(new[]
        {
            new Dummy { Id = 10, Name = "ten" },
            new Dummy { Id = 11, Name = "eleven" }
        });

        Assert.Equal(2, FakeConnection.LastCommand!.Executions);
    }

    private static OracleBulkWriterOptions<Dummy> CreateOptions() => new()
    {
        CommandText = "INSERT INTO items (id, name) VALUES (:p_id, :p_name)",
        ParameterNames = new[] { "p_id", "p_name" },
        ParameterDbTypes = new DbType?[] { DbType.Int32, DbType.String },
        ValueSelector = d => new object?[] { d.Id, d.Name }
    };

    private sealed class Dummy
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class FakeConnection : DbConnection
    {
        public static FakeCommand? LastCommand { get; private set; }
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

        protected override DbCommand CreateDbCommand()
        {
            LastCommand = new FakeCommand();
            return LastCommand;
        }

        public static void Reset() => LastCommand = null;
    }

    private sealed class FakeCommand : DbCommand
    {
        public int ArrayBindCount { get; set; } = 0;
        public int Executions { get; private set; }

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override UpdateRowSource UpdatedRowSource { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        protected override DbConnection? DbConnection { get; set; } = null!;
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery()
        {
            Executions++;
            return 0;
        }
        public override object ExecuteScalar() => new object();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            Executions++;
            return Task.FromResult(0);
        }
    }

    private sealed class FakeParameter : DbParameter
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
        public override bool Contains(object value) => _parameters.Contains((DbParameter)value);
        public override bool Contains(string value) => _parameters.Exists(p => p.ParameterName == value);
        public override void CopyTo(Array array, int index) => _parameters.ToArray().CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();
        public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _parameters.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _parameters.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _parameters.RemoveAt(index);
        public override void RemoveAt(string parameterName)
        {
            var idx = IndexOf(parameterName);
            if (idx >= 0)
            {
                _parameters.RemoveAt(idx);
            }
        }
        protected override DbParameter GetParameter(int index) => _parameters[index];
        protected override DbParameter GetParameter(string parameterName) => _parameters[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var idx = IndexOf(parameterName);
            if (idx >= 0)
            {
                _parameters[idx] = value;
            }
        }
    }
}

#pragma warning restore CS8765
