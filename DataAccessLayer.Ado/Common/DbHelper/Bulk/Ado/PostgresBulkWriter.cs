using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using Shared.Configuration;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Uses PostgreSQL COPY (binary) for high-throughput inserts.
/// </summary>
/// <typeparam name="T">Row type.</typeparam>
public sealed class PostgresBulkWriter<T> : IBulkWriter<T>
{
    private readonly Func<DbConnection>? _connectionFactory;
    private readonly DbConnection? _sharedConnection;
    private readonly PostgresBulkWriterOptions<T> _options;
    private readonly IPostgresCopyClientFactory _clientFactory;

    public PostgresBulkWriter(
        IDbConnectionFactory connectionFactory,
        DatabaseOptions defaultOptions,
        PostgresBulkWriterOptions<T> options,
        IPostgresCopyClientFactory? clientFactory = null)
        : this(
            () => connectionFactory.CreateConnection(options.OverrideOptions ?? defaultOptions),
            options,
            clientFactory)
    {
    }

    public PostgresBulkWriter(
        Func<DbConnection> connectionFactory,
        PostgresBulkWriterOptions<T> options,
        IPostgresCopyClientFactory? clientFactory = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _options = ValidateOptions(options);
        _clientFactory = clientFactory ?? new PostgresCopyClientFactory();
    }

    internal PostgresBulkWriter(
        DbConnection connection,
        PostgresBulkWriterOptions<T> options,
        IPostgresCopyClientFactory clientFactory)
    {
        _sharedConnection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = ValidateOptions(options);
        _clientFactory = clientFactory ?? new PostgresCopyClientFactory();
    }

    public void Write(IEnumerable<T> rows) =>
        WriteInternalAsync(rows, CancellationToken.None).GetAwaiter().GetResult();

    public Task WriteAsync(IEnumerable<T> rows, CancellationToken cancellationToken = default) =>
        WriteInternalAsync(rows, cancellationToken);

    private async Task WriteInternalAsync(IEnumerable<T> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (_sharedConnection is not null)
        {
            await ExecuteOnConnectionAsync(_sharedConnection, rows, cancellationToken).ConfigureAwait(false);
            return;
        }

        var connection = _connectionFactory!();
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteOnConnectionAsync(connection, rows, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteOnConnectionAsync(DbConnection connection, IEnumerable<T> rows, CancellationToken cancellationToken)
    {
        var client = _clientFactory.Create(connection, BuildCopyCommand());
        client.ConfigureColumns(_options.Columns);

        await using (client.ConfigureAwait(false))
        {
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await client.WriteRowAsync(ProjectRow(row), cancellationToken).ConfigureAwait(false);
            }

            await client.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private object?[] ProjectRow(T row)
    {
        var values = _options.ValueSelector?.Invoke(row)
            ?? throw new BulkOperationException("ValueSelector must be provided.");

        if (values.Length != _options.ColumnNames.Count)
        {
            throw new BulkOperationException("ValueSelector result length must match ColumnNames.");
        }

        return values;
    }

    private string BuildCopyCommand()
    {
        if (!string.IsNullOrWhiteSpace(_options.CopyCommand))
        {
            return _options.CopyCommand!;
        }

        var columns = string.Join(",", _options.ColumnNames.Select(QuoteIdentifier));
        return $"COPY {QuoteIdentifier(_options.DestinationTable!)} ({columns}) FROM STDIN (FORMAT BINARY)";
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier cannot be null or whitespace.", nameof(identifier));
        }

        return identifier.Contains('"', StringComparison.Ordinal)
            ? identifier
            : $"\"{identifier}\"";
    }

    private static PostgresBulkWriterOptions<T> ValidateOptions(PostgresBulkWriterOptions<T> options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.ColumnNames.Count == 0)
        {
            throw new ArgumentException("At least one column must be specified.", nameof(options));
        }

        if (options.ValueSelector is null)
        {
            throw new ArgumentException("ValueSelector is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.CopyCommand) &&
            string.IsNullOrWhiteSpace(options.DestinationTable))
        {
            throw new ArgumentException("DestinationTable is required when CopyCommand is not provided.", nameof(options));
        }

        return options;
    }
}
