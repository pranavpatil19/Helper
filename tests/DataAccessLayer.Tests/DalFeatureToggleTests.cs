using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using DataAccessLayer.Configuration;
using DataAccessLayer.Execution;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Mapping;
using DataAccessLayer.Telemetry;
using DataAccessLayer.Validation;
using DataAccessLayer.Database.ECM.DbContexts;
using DataAccessLayer.Database.ECM.Interfaces;
using DataAccessLayer.Database.ECM.Services;
using DataAccessLayer.Transactions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class DalFeatureToggleTests
{
    [Fact]
    public void TelemetryDisabled_RegistersNoOpTelemetry()
    {
        using var provider = BuildServiceProvider(DalFeatures.Default with { Telemetry = false });

        var telemetry = provider.GetRequiredService<IDataAccessTelemetry>();

        Assert.IsType<NoOpDataAccessTelemetry>(telemetry);
    }

    [Fact]
    public void ResilienceDisabled_RegistersNoOpStrategy()
    {
        using var provider = BuildServiceProvider(DalFeatures.Default with { Resilience = false });

        var strategy = provider.GetRequiredService<IResilienceStrategy>();

        Assert.IsType<NoOpResilienceStrategy>(strategy);
    }

    [Fact]
    public void BulkEnginesDisabled_DoesNotRegisterBulkHelper()
    {
        using var provider = BuildServiceProvider(DalFeatures.Default with { BulkEngines = false });

        Assert.Null(provider.GetService<IBulkWriteHelper>());
    }

    [Fact]
    public void EfHelpersDisabled_MakesAddEcmEntityFrameworkSupportNoOp()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Helper;Integrated Security=true;",
            WrapProviderExceptions = false
        };

        using var featureOverride = DalFeatureDefaults.Override(_ => DalFeatures.Default with { EfHelpers = false });

        services.AddDataAccessLayer(options);

        services.AddEcmEntityFrameworkSupport(options);

        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IEcmDbContextFactory>());
        Assert.Null(provider.GetService<ITodoRepository>());
    }

    [Fact]
    public void BulkProviderWhitelist_RegistersOnlyAllowedEngines()
    {
        using var provider = BuildServiceProvider(DalFeatures.Default with
        {
            EnabledBulkProviders = new HashSet<DatabaseProvider> { DatabaseProvider.SqlServer }
        });

        var engines = provider.GetServices<IBulkEngine>().ToList();

        var engine = Assert.Single(engines);
        Assert.IsType<SqlServerBulkEngine>(engine);
    }

    [Fact]
    public void DatabaseHelper_DetailedLoggingDisabled_SuppressesInfoLogs()
    {
        var command = new TestDbCommand();
        var logger = new TestLogger<DatabaseHelper>();
        var helper = CreateHelper(command, logger, DalFeatures.Default with { DetailedLogging = false });

        helper.Execute(new DbCommandRequest { CommandText = "dbo.test_proc" });

        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Information);
    }

    [Fact]
    public void DatabaseHelper_DetailedLoggingEnabled_EmitsInfoLogs()
    {
        var command = new TestDbCommand();
        var logger = new TestLogger<DatabaseHelper>();
        var helper = CreateHelper(command, logger, DalFeatures.Default with { DetailedLogging = true });

        helper.Execute(new DbCommandRequest { CommandText = "dbo.test_proc" });

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("Executed command", StringComparison.OrdinalIgnoreCase));
    }

    private static ServiceProvider BuildServiceProvider(DalFeatures? features = null)
    {
        using var featureOverride = features is null ? null : DalFeatureDefaults.Override(_ => features);
        var services = new ServiceCollection();
        services.AddLogging();

        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Helper;Integrated Security=true;",
            WrapProviderExceptions = false
        };

        services.AddDataAccessLayer(options);

        return services.BuildServiceProvider();
    }

    private static DatabaseHelper CreateHelper(
        TestDbCommand command,
        ILogger<DatabaseHelper> logger,
        DalFeatures features)
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
        var selectedFeatures = features ?? DalFeatures.Default;
        var helperOptions = new DbHelperOptions();
        var mapperFactory = new RowMapperFactory(helperOptions);

        return new DatabaseHelper(
            scopeManager,
            new StubCommandFactory(command),
            options,
            resilience,
            logger,
            telemetry,
            selectedFeatures,
            Array.Empty<IValidator<DbCommandRequest>>(),
            mapperFactory);
    }

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
        public DbCommand Rent(DbConnection connection, DbCommandRequest request)
        {
            _command.CommandText = request.CommandText;
            return _command;
        }

        public Task<DbCommand> RentAsync(DbConnection connection, DbCommandRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rent(connection, request));

        public void Return(DbCommand command) { }
    }

    private sealed class TestDbConnection : DbConnection
    {
        private readonly DbCommand _command;
        private ConnectionState _state = ConnectionState.Closed;

        public TestDbConnection(DbCommand command) => _command = command;

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Test";
        public override string DataSource => "Test";
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

    private sealed class TestDbCommand : DbCommand
    {
        private readonly TestParameterCollection _parameters = new();

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.StoredProcedure;
        public override UpdateRowSource UpdatedRowSource { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        protected override DbConnection? DbConnection { get; set; } = null!;
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 1;
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(ExecuteNonQuery());
        public override object ExecuteScalar() => 1;
        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult<object?>(ExecuteScalar());
        protected override DbParameter CreateDbParameter() => new TestDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new EmptyReader();
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
            Task.FromResult<DbDataReader>(new EmptyReader());
        public override void Prepare() { }
    }

    private sealed class TestParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _parameters = new();

        public override int Count => _parameters.Count;
        public override object SyncRoot { get; } = new();
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
        public override bool Contains(string value) => _parameters.Exists(p => string.Equals(p.ParameterName, value, StringComparison.OrdinalIgnoreCase));
        public override void CopyTo(Array array, int index) => _parameters.ToArray().CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();
        public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _parameters.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
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
                Add(value);
            }
        }
    }

    private sealed class TestDbParameter : DbParameter
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

    private sealed class EmptyReader : DbDataReader
    {
        public override int FieldCount => 0;
        public override int Depth => 0;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override object this[int ordinal] => throw new IndexOutOfRangeException();
        public override object this[string name] => throw new IndexOutOfRangeException();
        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => throw new NotSupportedException();
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
        public override object GetValue(int ordinal) => DBNull.Value;
        public override int GetValues(object[] values) => 0;
        public override bool IsDBNull(int ordinal) => true;
        public override bool NextResult() => false;
        public override bool Read() => false;
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
    }
}
