using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Data;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using DataAccessLayer;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using DataAccessLayer.Configuration;
using DataAccessLayer.Execution;
using DataAccessLayer.Mapping;
using DataAccessLayer.Telemetry;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Configuration;
using Xunit;

#nullable enable

namespace DataAccessLayer.Tests.DependencyInjection;

public sealed class DalServiceRegistrationTests
{
    [Fact]
    public void AddDataAccessLayer_DefaultTelemetry_IsNoOp()
    {
        using var provider = BuildServiceProvider();
        var telemetry = provider.GetRequiredService<IDataAccessTelemetry>();

        Assert.IsType<NoOpDataAccessTelemetry>(telemetry);
    }

    [Fact]
    public void AddDataAccessLayer_TelemetryEnabled_RegistersRealImplementation()
    {
        using var provider = BuildServiceProvider(options => options.EnableTelemetry = true);
        var telemetry = provider.GetRequiredService<IDataAccessTelemetry>();

        Assert.IsType<DataAccessTelemetry>(telemetry);
    }

    [Fact]
    public void AddDataAccessLayer_DefaultResilience_IsNoOp()
    {
        using var provider = BuildServiceProvider();
        var strategy = provider.GetRequiredService<IResilienceStrategy>();

        Assert.IsType<NoOpResilienceStrategy>(strategy);
    }

    [Fact]
    public void AddDataAccessLayer_ResilienceEnabled_RegistersRealStrategy()
    {
        using var provider = BuildServiceProvider(options => options.EnableResilience = true);
        var strategy = provider.GetRequiredService<IResilienceStrategy>();

        Assert.IsType<ResilienceStrategy>(strategy);
    }

    [Fact]
    public void AddDataAccessLayer_AlwaysRegistersBulkHelper()
    {
        using var provider = BuildServiceProvider();

        Assert.NotNull(provider.GetService<IBulkWriteHelper>());
        Assert.NotEmpty(provider.GetServices<IBulkEngine>());
    }

    [Fact]
    public void DatabaseHelper_DetailedLoggingFollowsRuntimeOption()
    {
        var command = new TestDbCommand();
        var logger = new TestLogger<DatabaseHelper>();
        var runtimeOptions = new DalRuntimeOptions { EnableDetailedLogging = true };
        var helperVerbose = CreateHelper(command, logger, runtimeOptions);

        helperVerbose.Execute(new DbCommandRequest { CommandText = "dbo.test_proc" });
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information);

        logger.Entries.Clear();
        var helperQuiet = CreateHelper(command, logger, new DalRuntimeOptions());
        helperQuiet.Execute(new DbCommandRequest { CommandText = "dbo.test_proc" });
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Information);
    }

    private static ServiceProvider BuildServiceProvider(Action<DalServiceRegistrationOptions>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Helper;Integrated Security=true;",
            WrapProviderExceptions = false
        };

        services.AddDataAccessLayer(options, configureServices: configureServices);
        return services.BuildServiceProvider();
    }

    private static DatabaseHelper CreateHelper(
        TestDbCommand command,
        ILogger<DatabaseHelper> logger,
        DalRuntimeOptions runtimeOptions)
    {
        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Helper;Integrated Security=true;",
            WrapProviderExceptions = false
        };
        var connectionFactory = new StubConnectionFactory(new TestDbConnection(command));
        var scopeManager = new ConnectionScopeManager(connectionFactory, options);
        var resilience = new ResilienceStrategy(options.Resilience, NullLogger<ResilienceStrategy>.Instance);
        var telemetry = new NoOpDataAccessTelemetry();
        var helperOptions = new DbHelperOptions();
        var mapperFactory = new RowMapperFactory(helperOptions);

        return new DatabaseHelper(
            scopeManager,
            new StubCommandFactory(command),
            options,
            resilience,
            logger,
            telemetry,
            runtimeOptions,
            Array.Empty<IValidator<DbCommandRequest>>(),
            mapperFactory);
    }

    #region Test doubles

    private sealed class StubConnectionFactory : IDbConnectionFactory
    {
        private readonly DbConnection _connection;
        public StubConnectionFactory(DbConnection connection) => _connection = connection;
        public DbConnection CreateConnection(DatabaseOptions options) => _connection;
    }

    private sealed class StubCommandFactory : IDbCommandFactory
    {
        private readonly TestDbCommand _command;
        public StubCommandFactory(TestDbCommand command) => _command = command;

        public DbCommand GetCommand(DbConnection connection, DbCommandRequest request)
        {
            _command.CommandText = request.CommandText;
            return _command;
        }

        public Task<DbCommand> GetCommandAsync(DbConnection connection, DbCommandRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetCommand(connection, request));

        public void ReturnCommand(DbCommand command) { }
    }

    private sealed class TestDbConnection : DbConnection
    {
        private readonly DbCommand _command;
        private ConnectionState _state = ConnectionState.Closed;

        public TestDbConnection(DbCommand command) => _command = command;

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => _state;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => _command;
    }

    private sealed class TestDbCommand : DbCommand
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new TestParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; } = false;

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 1;
        public override object ExecuteScalar() => 1;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new TestDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
            Task.FromResult<DbDataReader>(new TestDbDataReader());
    }

    private sealed class TestParameterCollection : DbParameterCollection
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
        protected override DbParameter GetParameter(int index) => throw new NotSupportedException();
        protected override DbParameter GetParameter(string parameterName) => throw new NotSupportedException();
        protected override void SetParameter(int index, DbParameter value) => throw new NotSupportedException();
        protected override void SetParameter(string parameterName, DbParameter value) => throw new NotSupportedException();
    }

    private sealed class TestDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [AllowNull]
        public override string ParameterName { get; set; } = string.Empty;
        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;
        [AllowNull]
        public override object Value { get; set; } = string.Empty;
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class TestDbDataReader : DbDataReader
    {
        public override bool HasRows => false;
        public override int FieldCount => 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;
        public override object this[int ordinal] => throw new NotSupportedException();
        public override object this[string name] => throw new NotSupportedException();
        public override bool GetBoolean(int ordinal) => false;
        public override byte GetByte(int ordinal) => 0;
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => default;
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
        public override object GetValue(int ordinal) => string.Empty;
        public override int GetValues(object[] values) => 0;
        public override bool IsDBNull(int ordinal) => true;
        public override bool NextResult() => false;
        public override bool Read() => false;
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<TestLoggerEntry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NullScope();
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new TestLoggerEntry(logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public void Dispose() { }
        }
    }

    private sealed record TestLoggerEntry(LogLevel Level, string Message);

    #endregion
}
