using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Configuration;
using DataAccessLayer.Mapping;
using DataAccessLayer.Telemetry;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests;

#nullable enable

public sealed class DatabaseHelperResilienceTests
{
    [Fact]
    public async Task ExecuteAsync_Retries_OnTransientFailure()
    {
        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=.;Database=Fake;Trusted_Connection=True;",
            Resilience = new ResilienceOptions
            {
                EnableCommandRetries = true,
                CommandRetryCount = 2,
                CommandBaseDelayMilliseconds = 1
            }
        };

        var connectionFactory = new ResilientConnectionFactory();
        var scopeManager = new ConnectionScopeManager(connectionFactory, options);
        var helperOptions = new DbHelperOptions();
        var telemetry = new DataAccessTelemetry(helperOptions);
        var rowMapperFactory = new RowMapperFactory(helperOptions);
        var features = DalFeatures.Default;
        var helper = new DatabaseHelper(
            scopeManager,
            new TransientFailureCommandFactory(),
            options,
            CreateResilienceStrategy(options.Resilience),
            NullLogger<DatabaseHelper>.Instance,
            telemetry,
            features,
            Array.Empty<IValidator<DbCommandRequest>>(),
            rowMapperFactory);

        var request = new DbCommandRequest { CommandText = "SELECT 1" };

        var result = await helper.ExecuteAsync(request);

        Assert.Equal(1, result.RowsAffected);
    }

    private static IResilienceStrategy CreateResilienceStrategy(ResilienceOptions options) =>
        new ResilienceStrategy(options, NullLogger<ResilienceStrategy>.Instance);

    private sealed class ResilientConnectionFactory : IDbConnectionFactory
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
        protected override DbCommand CreateDbCommand() => new SuccessfulDbCommand();
    }

    private sealed class TransientFailureCommandFactory : IDbCommandFactory
    {
        private readonly SuccessfulDbCommand _command = new();

        public DbCommand Rent(DbConnection connection, DbCommandRequest request)
        {
            _command.Attach(connection);
            return _command;
        }

        public Task<DbCommand> RentAsync(DbConnection connection, DbCommandRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Rent(connection, request));

        public void Return(DbCommand command) { }
    }

    private sealed class SuccessfulDbCommand : DbCommand
    {
        private int _attempt;

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override UpdateRowSource UpdatedRowSource { get; set; }
            = UpdateRowSource.None;
        [System.Diagnostics.CodeAnalysis.AllowNull]
        protected override DbConnection? DbConnection { get; set; } = null!;
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }
            = null;
        public override bool DesignTimeVisible { get; set; }
            = false;

        public void Attach(DbConnection connection) => DbConnection = connection;

        public override void Cancel() { }
        public override int ExecuteNonQuery()
        {
            if (_attempt++ == 0)
            {
                throw new TimeoutException("Simulated transient failure.");
            }

            return 1;
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            try
            {
                var rows = ExecuteNonQuery();
                return Task.FromResult(rows);
            }
            catch (Exception ex)
            {
                return Task.FromException<int>(ex);
            }
        }

        public override object ExecuteScalar() => ExecuteNonQuery();
        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
            => Task.FromResult<object?>(ExecuteScalar());
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
            Task.FromResult<DbDataReader>(new FakeDbDataReader());
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
        protected override DbParameter GetParameter(int index) => new FakeParameter();
        protected override DbParameter GetParameter(string parameterName) => new FakeParameter();
        protected override void SetParameter(int index, DbParameter value) { }
        protected override void SetParameter(string parameterName, DbParameter value) { }
    }

    private sealed class FakeDbDataReader : DbDataReader
    {
        public override bool Read() => false;
        public override int FieldCount => 0;
        public override object this[int ordinal] => null!;
        public override object this[string name] => null!;
        public override int Depth => 0;
        public override bool IsClosed => true;
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
