#nullable enable

using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Exceptions;
using DataAccessLayer.Execution;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Configuration;
using DataAccessLayer.Mapping;
using DataAccessLayer.Telemetry;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Configuration;
using Xunit;
using DataException = DataAccessLayer.Exceptions.DataException;

namespace DataAccessLayer.Tests.DbHelper.Exceptions;

public sealed class ExceptionWrappingTests
{
    [Fact]
    public async Task ExecuteAsync_WrapsExceptions_WhenEnabled()
    {
        var options = CreateOptions(wrap: true);
        var helper = CreateHelper(options);
        var request = new DbCommandRequest { CommandText = "SELECT 1" };

        var ex = await Assert.ThrowsAsync<DataException>(() => helper.ExecuteAsync(request));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public async Task ExecuteAsync_AllowsRawExceptions_WhenDisabled()
    {
        var options = CreateOptions(wrap: false);
        var helper = CreateHelper(options);
        var request = new DbCommandRequest { CommandText = "SELECT 1" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => helper.ExecuteAsync(request));
    }

    private static DatabaseHelper CreateHelper(DatabaseOptions options)
    {
        var connectionFactory = new FakeConnectionFactory();
        var scopeManager = new ConnectionScopeManager(connectionFactory, options);
        var helperOptions = new DbHelperOptions();
        var telemetry = new DataAccessTelemetry(helperOptions);
        var rowMapperFactory = new RowMapperFactory(helperOptions);
        var runtimeOptions = new DalRuntimeOptions();
        return new DatabaseHelper(
            scopeManager,
            new ThrowingCommandFactory(),
            options,
            new ResilienceStrategy(options.Resilience, NullLogger<ResilienceStrategy>.Instance),
            NullLogger<DatabaseHelper>.Instance,
            telemetry,
            runtimeOptions,
            Array.Empty<IValidator<DbCommandRequest>>(),
            rowMapperFactory);
    }

    private static DatabaseOptions CreateOptions(bool wrap) => new()
    {
        Provider = DatabaseProvider.SqlServer,
        ConnectionString = "Server=.;Database=Fake;Trusted_Connection=True;",
        WrapProviderExceptions = wrap
    };

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
        protected override DbCommand CreateDbCommand() => new ThrowingCommand();
    }

    private sealed class ThrowingCommandFactory : IDbCommandFactory
    {
        public DbCommand GetCommand(DbConnection connection, DbCommandRequest request) => new ThrowingCommand { Connection = connection };
        public Task<DbCommand> GetCommandAsync(DbConnection connection, DbCommandRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(GetCommand(connection, request));
        public void ReturnCommand(DbCommand command) => command.Dispose();
    }

    private sealed class ThrowingCommand : DbCommand
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override UpdateRowSource UpdatedRowSource { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        protected override DbConnection? DbConnection { get; set; } = null!;
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeParameterCollection();
        public override bool DesignTimeVisible { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new InvalidOperationException("boom");
        public override object ExecuteScalar() => throw new InvalidOperationException("boom");
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new InvalidOperationException("boom");
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
            Task.FromException<DbDataReader>(new InvalidOperationException("boom"));
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) =>
            Task.FromException<int>(new InvalidOperationException("boom"));
        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) =>
            Task.FromException<object?>(new InvalidOperationException("boom"));
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
}
