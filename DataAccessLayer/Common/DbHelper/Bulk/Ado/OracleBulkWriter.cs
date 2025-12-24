using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using Shared.Configuration;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Uses Oracle array binding to execute high-throughput bulk DML.
/// </summary>
/// <typeparam name="T">Row type.</typeparam>
public sealed class OracleBulkWriter<T> : IBulkWriter<T>
{
    private readonly Func<DbConnection>? _connectionFactory;
    private readonly DbConnection? _sharedConnection;
    private readonly OracleBulkWriterOptions<T> _options;

    public OracleBulkWriter(
        IDbConnectionFactory connectionFactory,
        DatabaseOptions defaultOptions,
        OracleBulkWriterOptions<T> options)
        : this(
            () => connectionFactory.CreateConnection(options.OverrideOptions ?? defaultOptions),
            options)
    {
    }

    public OracleBulkWriter(
        Func<DbConnection> connectionFactory,
        OracleBulkWriterOptions<T> options)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _options = ValidateOptions(options);
    }

    internal OracleBulkWriter(
        DbConnection connection,
        OracleBulkWriterOptions<T> options)
    {
        _sharedConnection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = ValidateOptions(options);
    }

    public void Write(IEnumerable<T> rows) =>
        ExecuteInternalAsync(rows, CancellationToken.None).GetAwaiter().GetResult();

    public Task WriteAsync(IEnumerable<T> rows, CancellationToken cancellationToken = default) =>
        ExecuteInternalAsync(rows, cancellationToken);

    private async Task ExecuteInternalAsync(IEnumerable<T> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (_sharedConnection is not null)
        {
            await ExecuteWithConnectionAsync(_sharedConnection, rows, cancellationToken).ConfigureAwait(false);
            return;
        }

        var connection = _connectionFactory!();
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteWithConnectionAsync(connection, rows, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteWithConnectionAsync(DbConnection connection, IEnumerable<T> rows, CancellationToken cancellationToken)
    {
        var command = PrepareCommand(connection);
        await using (command.ConfigureAwait(false))
        {
            await ExecuteBatchesAsync(rows, command, cancellationToken).ConfigureAwait(false);
        }
    }

    private DbCommand PrepareCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = _options.CommandText;
        command.CommandType = _options.CommandType;
        return command;
    }

    private async Task ExecuteBatchesAsync(
        IEnumerable<T> rows,
        DbCommand command,
        CancellationToken cancellationToken)
    {
        var buffer = new List<object?[]>(_options.BatchSize);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            buffer.Add(ProjectRow(row));
            if (buffer.Count == _options.BatchSize)
            {
                BindParameters(command, buffer);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            BindParameters(command, buffer);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private object?[] ProjectRow(T row)
    {
        var values = _options.ValueSelector?.Invoke(row)
            ?? throw new BulkOperationException("ValueSelector must be provided.");

        if (values.Length != _options.ParameterNames.Count)
        {
            throw new BulkOperationException("Value selector result length must match ParameterNames.");
        }

        return values;
    }

    private void BindParameters(
        DbCommand command,
        IReadOnlyList<object?[]> batch)
    {
        SetArrayBindCount(command, batch.Count);

        for (var columnIndex = 0; columnIndex < _options.ParameterNames.Count; columnIndex++)
        {
            var columnValues = new object?[batch.Count];
            for (var rowIndex = 0; rowIndex < batch.Count; rowIndex++)
            {
                columnValues[rowIndex] = batch[rowIndex][columnIndex];
            }

            var parameter = GetOrCreateParameter(command, columnIndex);
            parameter.Value = columnValues;
        }
    }

    private DbParameter GetOrCreateParameter(DbCommand command, int columnIndex)
    {
        if (columnIndex < command.Parameters.Count)
        {
            return command.Parameters[columnIndex];
        }

        var parameter = command.CreateParameter();
        parameter.ParameterName = _options.ParameterNames[columnIndex];
        parameter.Direction = ParameterDirection.Input;

        if (_options.ParameterDbTypes is { Count: > 0 } types &&
            columnIndex < types.Count &&
            types[columnIndex] is { } dbType)
        {
            parameter.DbType = dbType;
        }

        command.Parameters.Add(parameter);
        return parameter;
    }

    private static void SetArrayBindCount(DbCommand command, int count)
    {
        var property = command.GetType().GetProperty("ArrayBindCount", BindingFlags.Public | BindingFlags.Instance);
        property?.SetValue(command, count);
    }

    private static OracleBulkWriterOptions<T> ValidateOptions(OracleBulkWriterOptions<T> options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.CommandText))
        {
            throw new ArgumentException("CommandText is required.", nameof(options));
        }

        if (options.ParameterNames.Count == 0)
        {
            throw new ArgumentException("At least one parameter must be defined.", nameof(options));
        }

        if (options.ValueSelector is null)
        {
            throw new ArgumentException("ValueSelector is required.", nameof(options));
        }

        if (options.BatchSize <= 0)
        {
            throw new ArgumentException("BatchSize must be greater than zero.", nameof(options));
        }

        return options;
    }
}
