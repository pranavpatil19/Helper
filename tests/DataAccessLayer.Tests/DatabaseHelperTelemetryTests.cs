using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
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

public sealed class DatabaseHelperTelemetryTests
{
    [Fact]
    public async Task ExecuteAsync_EmitsActivity()
    {
        Activity? recorded = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Helper.DataAccessLayer.Database",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity =>
            {
                if (activity.DisplayName == "ExecuteAsync")
                {
                    recorded = activity;
                }
            }
        };
        ActivitySource.AddActivityListener(listener);

        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=.;Database=Fake;Trusted_Connection=True;"
        };

        var connectionFactory = new StubConnectionFactory();
        var scopeManager = new ConnectionScopeManager(connectionFactory, options);
        var helperOptions = new DbHelperOptions();
        var telemetry = new DataAccessTelemetry(helperOptions);
        var rowMapperFactory = new RowMapperFactory(helperOptions);
        var features = DalFeatures.Default;
        var helper = new DatabaseHelper(
            scopeManager,
            new SuccessfulCommandFactory(),
            options,
            new ResilienceStrategy(options.Resilience, NullLogger<ResilienceStrategy>.Instance),
            NullLogger<DatabaseHelper>.Instance,
            telemetry,
            features,
            Array.Empty<IValidator<DbCommandRequest>>(),
            rowMapperFactory);

        var request = new DbCommandRequest { CommandText = "SELECT 1" };

        var result = await helper.ExecuteAsync(request);

        Assert.Equal(1, result.RowsAffected);
        Assert.NotNull(recorded);
        Assert.Equal("ExecuteAsync", recorded!.DisplayName);
    }

    private sealed class StubConnectionFactory : IDbConnectionFactory
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

    private sealed class SuccessfulCommandFactory : IDbCommandFactory
    {
        public DbCommand Rent(DbConnection connection, DbCommandRequest request)
        {
            var command = new SuccessfulDbCommand();
            command.Attach(connection);
            return command;
        }

        public Task<DbCommand> RentAsync(DbConnection connection, DbCommandRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rent(connection, request));

        public void Return(DbCommand command) { }
    }

    private sealed class SuccessfulDbCommand : DbCommand
    {
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
        public override int ExecuteNonQuery() => 1;
        public override object ExecuteScalar() => 1;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
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

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _parameters = new();

        public override int Count => _parameters.Count;
        public override object SyncRoot => ((ICollection)_parameters).SyncRoot;
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
        public override bool Contains(string value) => _parameters.Exists(p => string.Equals(p.ParameterName, value, StringComparison.Ordinal));
        public override void CopyTo(Array array, int index) => ((ICollection)_parameters).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();
        public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _parameters.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.Ordinal));
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
        protected override DbParameter GetParameter(string parameterName)
            => _parameters[IndexOf(parameterName)];
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
                _parameters.Add(value);
            }
        }
    }
}
