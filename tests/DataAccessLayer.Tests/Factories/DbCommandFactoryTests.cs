using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using DataAccessLayer.Execution;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Providers.Oracle;
using Microsoft.Extensions.Logging;
using Shared.Configuration;
using Xunit;
using DalParameter = DataAccessLayer.Execution.Builders.DbParameter;

namespace DataAccessLayer.Tests.Factories;

#nullable enable

#pragma warning disable CS8765 // test doubles deliberately relax nullability
#pragma warning disable CS0108 // test doubles shadow base members

public sealed class DbCommandFactoryTests
{
    [Fact]
    public void GetCommandAndReturn_ReusesCommandInstances()
    {
        var factory = new DbCommandFactory(
            new NoOpParameterBinder(),
            new NoOpParameterPool(),
            CreateDefaults(),
            new CommandPoolOptions());
        var connection = new FakeConnection();
        connection.Open();
        var request = new DbCommandRequest { CommandText = "SELECT 1" };

        var command1 = factory.GetCommand(connection, request);
        factory.ReturnCommand(command1);

        var command2 = factory.GetCommand(connection, request);
        Assert.Same(command1, command2);
    }

    [Fact]
    public void GetCommand_DisabledPooling_DoesNotReuse()
    {
        var options = new CommandPoolOptions { EnableCommandPooling = false };
        var factory = new DbCommandFactory(
            new NoOpParameterBinder(),
            new NoOpParameterPool(),
            CreateDefaults(),
            options);
        var connection = new FakeConnection();
        connection.Open();
        var request = new DbCommandRequest { CommandText = "SELECT 1" };

        var command1 = factory.GetCommand(connection, request);
        factory.ReturnCommand(command1);

        var command2 = factory.GetCommand(connection, request);
        Assert.NotSame(command1, command2);
    }

    [Fact]
    public void ParameterPooling_ReusesParameterInstances()
    {
        var options = new CommandPoolOptions
        {
            EnableParameterPooling = true
        };
        var parameterPool = new DbParameterPool(options);
        var binder = new ParameterBinder(parameterPool);
        var factory = new DbCommandFactory(binder, parameterPool, CreateDefaults(), options);
        var connection = new FakeConnection();
        connection.Open();
        var request = new DbCommandRequest
        {
            CommandText = "SELECT 1",
            Parameters = DalParameter.FromAnonymous(new { Id = 1 })
        };

        var command1 = factory.GetCommand(connection, request);
        var parameter1 = command1.Parameters[0];
        factory.ReturnCommand(command1);

        var command2 = factory.GetCommand(connection, request);
        var parameter2 = command2.Parameters[0];

        Assert.Same(parameter1, parameter2);
    }

    [Fact]
    public void GetCommand_AppliesDefaultCommandTimeout_WhenRequestDoesNotSpecify()
    {
        var defaults = CreateDefaults(commandTimeout: 90);
        var factory = new DbCommandFactory(
            new NoOpParameterBinder(),
            new NoOpParameterPool(),
            defaults,
            new CommandPoolOptions { EnableCommandPooling = false });
        var connection = new FakeConnection();
        connection.Open();
        var request = new DbCommandRequest { CommandText = "SELECT 1" };

        var command = factory.GetCommand(connection, request);

        Assert.Equal(90, command.CommandTimeout);
    }

    [Fact]
    public void GetCommand_AppliesOverrideCommandTimeout_BeforeDefaults()
    {
        var defaults = CreateDefaults(commandTimeout: 90);
        var overrides = CreateDefaults(commandTimeout: 15);
        var factory = new DbCommandFactory(
            new NoOpParameterBinder(),
            new NoOpParameterPool(),
            defaults,
            new CommandPoolOptions { EnableCommandPooling = false });
        var connection = new FakeConnection();
        connection.Open();
        var request = new DbCommandRequest
        {
            CommandText = "SELECT 1",
            OverrideOptions = overrides
        };

        var command = factory.GetCommand(connection, request);

        Assert.Equal(15, command.CommandTimeout);
    }

    [Fact]
    public void GetCommand_HonorsExplicitCommandTimeout_OnRequest()
    {
        var defaults = CreateDefaults(commandTimeout: 90);
        var factory = new DbCommandFactory(
            new NoOpParameterBinder(),
            new NoOpParameterPool(),
            defaults,
            new CommandPoolOptions { EnableCommandPooling = false });
        var connection = new FakeConnection();
        connection.Open();
        var request = new DbCommandRequest
        {
            CommandText = "SELECT 1",
            CommandTimeoutSeconds = 5
        };

        var command = factory.GetCommand(connection, request);

        Assert.Equal(5, command.CommandTimeout);
    }

    [Fact]
    public void GetCommand_LogsTimeout_WhenDiagnosticsEnabled()
    {
        var logger = new TestLogger<DbCommandFactory>();
        var defaults = CreateDefaults(commandTimeout: 30, logTimeouts: true);
        var factory = new DbCommandFactory(
            new NoOpParameterBinder(),
            new NoOpParameterPool(),
            defaults,
            new CommandPoolOptions { EnableCommandPooling = false },
            logger);
        var connection = new FakeConnection();
        connection.Open();
        var request = new DbCommandRequest { CommandText = "SELECT 1" };

        _ = factory.GetCommand(connection, request);

        Assert.Single(logger.Entries);
        Assert.Contains("30", logger.Entries[0].Message);
        Assert.Contains("SELECT 1", logger.Entries[0].Message);
    }

    [Fact]
    public void GetCommand_DoesNotLogTimeout_WhenDiagnosticsDisabled()
    {
        var logger = new TestLogger<DbCommandFactory>();
        var defaults = CreateDefaults(commandTimeout: 30, logTimeouts: false);
        var factory = new DbCommandFactory(
            new NoOpParameterBinder(),
            new NoOpParameterPool(),
            defaults,
            new CommandPoolOptions { EnableCommandPooling = false },
            logger);
        var connection = new FakeConnection();
        connection.Open();
        var request = new DbCommandRequest { CommandText = "SELECT 1" };

        _ = factory.GetCommand(connection, request);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void ListParameters_ExpandForSqlServerTextCommands()
    {
        var factory = new DbCommandFactory(
            new ParameterBinder(new NoOpParameterPool()),
            new NoOpParameterPool(),
            CreateDefaults());
        var connection = new FakeConnection();
        connection.Open();
        var request = new DbCommandRequest
        {
            CommandText = "SELECT * FROM Orders WHERE Id IN (@Ids)",
            Parameters = new[]
            {
                DalParameter.InputList("Ids", new[] { 1, 2, 3 })
            }
        };

        var command = factory.GetCommand(connection, request);

        Assert.Equal("SELECT * FROM Orders WHERE Id IN (@Ids_0,@Ids_1,@Ids_2)", command.CommandText);
        Assert.Equal(3, command.Parameters.Count);
        var first = command.Parameters[0];
        Assert.Equal("@Ids_0", first.ParameterName);
        Assert.Equal(1, first.Value);
    }

    [Fact]
    public void ListParameters_BecomeArrays_ForPostgres()
    {
        var defaults = CreateDefaults(DatabaseProvider.PostgreSql);
        var factory = new DbCommandFactory(
            new ParameterBinder(new NoOpParameterPool()),
            new NoOpParameterPool(),
            defaults,
            new CommandPoolOptions { EnableCommandPooling = false });
        var connection = new FakeConnection();
        connection.Open();
        var request = new DbCommandRequest
        {
            CommandText = "select * from accounts where id = any(@Ids)",
            Parameters =
            [
                DalParameter.InputList("Ids", new[] { 10, 20 })
            ]
        };

        var command = factory.GetCommand(connection, request);

        Assert.Single(command.Parameters);
        var parameter = command.Parameters[0];
        Assert.IsType<object?[]>(parameter.Value);
        var array = (object?[])parameter.Value!;
        Assert.Equal(new object?[] { 10, 20 }, array);
    }

    [Fact]
    public void ListParameters_BecomeArrays_ForOracle()
    {
        var defaults = CreateDefaults(DatabaseProvider.Oracle);
        var factory = new DbCommandFactory(
            new ParameterBinder(new NoOpParameterPool()),
            new NoOpParameterPool(),
            defaults,
            new CommandPoolOptions { EnableCommandPooling = false });
        var connection = new FakeConnection();
        connection.Open();
        var request = new DbCommandRequest
        {
            CommandText = "select * from dual where id in (:Ids)",
            Parameters =
            [
                DalParameter.InputList("Ids", new[] { "A", "B" })
            ]
        };

        var command = factory.GetCommand(connection, request);

        Assert.Single(command.Parameters);
        var parameter = command.Parameters[0];
        Assert.IsType<object?[]>(parameter.Value);
        var array = (object?[])parameter.Value!;
        Assert.Equal(new object?[] { "A", "B" }, array);
    }

    [Fact]
    public void StoredProcedureParameterMatrix_BindsAllDirections()
    {
        var defaults = CreateDefaults(DatabaseProvider.Oracle);
        var factory = new DbCommandFactory(
            new ParameterBinder(new NoOpParameterPool()),
            new NoOpParameterPool(),
            defaults,
            new CommandPoolOptions { EnableCommandPooling = false });
        var connection = new FakeConnection();
        connection.Open();

        var request = new DbCommandRequest
        {
            CommandText = "pkg_orders.process",
            CommandType = CommandType.StoredProcedure,
            Parameters =
            [
                DalParameter.Input("p_order_id", 7, DbType.Int32),
                new DbParameterDefinition
                {
                    Name = "p_tags",
                    Direction = ParameterDirection.Input,
                    TreatAsList = true,
                    Values = new object?[] { "rush", "bulk" },
                    ProviderTypeName = "SYS.ODCIVARCHAR2LIST"
                },
                DalParameter.Output("p_status", DbType.String, size: 32),
                new DbParameterDefinition
                {
                    Name = "p_amount",
                    Direction = ParameterDirection.InputOutput,
                    Value = 15.75m,
                    DbType = DbType.Decimal,
                    Precision = 18,
                    Scale = 2,
                    IsNullable = true
                },
                DalParameter.ReturnValue("p_return_code", DbType.Int32),
                OracleParameterHelper.RefCursor("p_cursor")
            ]
        };

        var command = factory.GetCommand(connection, request);

        Assert.Equal(6, command.Parameters.Count);
        var parameters = command.Parameters.Cast<DbParameter>().ToDictionary(p => p.ParameterName);

        var orderParam = parameters[":p_order_id"];
        Assert.Equal(ParameterDirection.Input, orderParam.Direction);
        Assert.Equal(DbType.Int32, orderParam.DbType);
        Assert.Equal(7, orderParam.Value);

        var tagsParam = parameters[":p_tags"];
        Assert.Equal(ParameterDirection.Input, tagsParam.Direction);
        var tagValues = Assert.IsType<object?[]>(tagsParam.Value);
        Assert.Collection(tagValues,
            value => Assert.Equal("rush", value),
            value => Assert.Equal("bulk", value));
        Assert.Equal("SYS.ODCIVARCHAR2LIST", ((FakeParameter)tagsParam).DataTypeName);

        var statusParam = parameters[":p_status"];
        Assert.Equal(ParameterDirection.Output, statusParam.Direction);
        Assert.Equal(DbType.String, statusParam.DbType);
        Assert.Equal(32, statusParam.Size);

        var amountParam = parameters[":p_amount"];
        Assert.Equal(ParameterDirection.InputOutput, amountParam.Direction);
        Assert.Equal(DbType.Decimal, amountParam.DbType);
        Assert.Equal(18, amountParam.Precision);
        Assert.Equal(2, amountParam.Scale);
        Assert.Equal(15.75m, amountParam.Value);

        var returnParam = parameters[":p_return_code"];
        Assert.Equal(ParameterDirection.ReturnValue, returnParam.Direction);
        Assert.Equal(DbType.Int32, returnParam.DbType);

        var cursorParam = parameters[":p_cursor"];
        Assert.Equal(ParameterDirection.Output, cursorParam.Direction);
        Assert.Equal(DbType.Object, cursorParam.DbType);
        Assert.Equal("RefCursor", ((FakeParameter)cursorParam).DataTypeName);
    }

    private static DatabaseOptions CreateDefaults(DatabaseProvider provider = DatabaseProvider.SqlServer, int? commandTimeout = null, bool logTimeouts = false)
    {
        return new DatabaseOptions
        {
            Provider = provider,
            ConnectionString = "Server=(local);Database=Test;Trusted_Connection=True;",
            CommandTimeoutSeconds = commandTimeout,
            Diagnostics = new DiagnosticsOptions
            {
                LogEffectiveTimeouts = logTimeouts
            }
        };
    }

    private sealed class NoOpParameterBinder : IParameterBinder
    {
        public void BindParameters(DbCommand command, IReadOnlyList<DbParameterDefinition> definitions, DatabaseProvider provider)
        {
            // no-op
        }
    }

    private sealed class NoOpParameterPool : IDbParameterPool
    {
        public bool IsEnabled => false;
        public DbParameter Rent(DbCommand command) => command.CreateParameter();
        public void Return(DbParameter parameter) { }
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

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; }
            = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        protected override DbConnection? DbConnection { get; set; } = null!;
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        public bool Disposed { get; private set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object ExecuteScalar() => new object();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new FakeReader();
        protected override void Dispose(bool disposing) => Disposed = true;
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
        public override byte Precision { get; set; }
        public override byte Scale { get; set; }
        public string? DataTypeName { get; set; } = string.Empty;
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
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index) => _parameters.ToArray().CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();
        public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _parameters.Remove((DbParameter)value);
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
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
    }

}

#pragma warning restore CS0108
#pragma warning restore CS8765
