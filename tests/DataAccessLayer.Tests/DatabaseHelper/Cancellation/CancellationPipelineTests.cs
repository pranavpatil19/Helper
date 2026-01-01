using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Configuration;
using DataAccessLayer.Execution;
using DataAccessLayer.Mapping;
using DataAccessLayer.Telemetry;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Configuration;
using Xunit;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DataAccessLayer.Tests.DbHelper.Cancellation;

public sealed class CancellationPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=.;Database=Helper;Integrated Security=true;",
            WrapProviderExceptions = false
        };

        var connectionFactory = new TrackingConnectionFactory();
        var scopeManager = new ConnectionScopeManager(connectionFactory, options);
        var helperOptions = new DbHelperOptions();
        var mapperFactory = new RowMapperFactory(helperOptions);
        var commandFactory = new TrackingCommandFactory();
        var helper = new DatabaseHelper(
            scopeManager,
            commandFactory,
            options,
            new ResilienceStrategy(options.Resilience, NullLogger<ResilienceStrategy>.Instance),
            NullLogger<DatabaseHelper>.Instance,
            new NoOpDataAccessTelemetry(),
            new DalRuntimeOptions(),
            Array.Empty<IValidator<DbCommandRequest>>(),
            mapperFactory);

        var request = new DbCommandRequest { CommandText = "SELECT 1" };
        using var cts = new CancellationTokenSource();

        var result = await helper.ExecuteAsync(request, cts.Token);

        Assert.Equal(1, result.RowsAffected);
        Assert.Equal(cts.Token, commandFactory.LastGetToken);
        Assert.Equal(cts.Token, commandFactory.Command!.LastExecutionToken);
    }

    private sealed class TrackingConnectionFactory : IDbConnectionFactory
    {
        private readonly TrackingConnection _connection = new();
        public DbConnection CreateConnection(DatabaseOptions options) => _connection;
    }

    private sealed class TrackingCommandFactory : IDbCommandFactory
    {
        public TrackingDbCommand? Command { get; private set; }
        public CancellationToken LastGetToken { get; private set; }

        public DbCommand GetCommand(DbConnection connection, DbCommandRequest request)
        {
            Command = new TrackingDbCommand();
            Command.Attach(connection);
            return Command;
        }

        public Task<DbCommand> GetCommandAsync(DbConnection connection, DbCommandRequest request, CancellationToken cancellationToken = default)
        {
            LastGetToken = cancellationToken;
            return Task.FromResult(GetCommand(connection, request));
        }

        public void ReturnCommand(DbCommand command) { }
    }

    private sealed class TrackingDbCommand : DbCommand
    {
        public CancellationToken LastExecutionToken { get; private set; }

        private string _commandText = string.Empty;

        public void Attach(DbConnection connection) => DbConnection = connection;

        [AllowNull]
        public override string CommandText
        {
            get => _commandText;
            set => _commandText = value ?? string.Empty;
        }
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.None;
        protected override DbConnection? DbConnection { get; set; }

        protected override DbParameterCollection DbParameterCollection { get; } = new TrackingParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 1;
        public override object ExecuteScalar() => 1;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new TrackingParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            LastExecutionToken = cancellationToken;
            return Task.FromResult(1);
        }

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            LastExecutionToken = cancellationToken;
            return Task.FromResult<object?>(1);
        }

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
            Task.FromResult<DbDataReader>(new TrackingDbDataReader());
    }

    private sealed class TrackingParameterCollection : DbParameterCollection
    {
        public override int Count => 0;
        public override object SyncRoot => this;
        public override int Add([AllowNull] object value) => 0;
        public override void AddRange(Array values) { }
        public override void Clear() { }
        public override bool Contains([AllowNull] object value) => false;
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index) { }
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
        public override int IndexOf([AllowNull] object value) => -1;
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, [AllowNull] object value) { }
        public override void Remove([AllowNull] object value) { }
        public override void RemoveAt(int index) { }
        public override void RemoveAt(string parameterName) { }
        protected override DbParameter GetParameter(int index) => throw new NotSupportedException();
        protected override DbParameter GetParameter(string parameterName) => throw new NotSupportedException();
        protected override void SetParameter(int index, [AllowNull] DbParameter value) => throw new NotSupportedException();
        protected override void SetParameter(string parameterName, [AllowNull] DbParameter value) => throw new NotSupportedException();
    }

    private sealed class TrackingParameter : DbParameter
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

    private sealed class TrackingDbDataReader : DbDataReader
    {
        public override int FieldCount => 0;
        public override bool HasRows => false;
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

    private sealed class TrackingConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Tracking";
        public override string DataSource => "Tracking";
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
        protected override DbCommand CreateDbCommand() => new TrackingDbCommand();
    }
}
