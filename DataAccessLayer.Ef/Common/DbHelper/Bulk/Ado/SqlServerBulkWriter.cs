using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;
using DataAccessLayer.Common.DbHelper;
using Microsoft.Data.SqlClient;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Uses SqlBulkCopy to insert rows efficiently into SQL Server.
/// </summary>
/// <typeparam name="T">Row type.</typeparam>
public sealed class SqlServerBulkWriter<T> : IBulkWriter<T>
{
    private readonly Func<DbConnection>? _connectionFactory;
    private readonly DbConnection? _sharedConnection;
    private readonly DbTransaction? _sharedTransaction;
    private readonly SqlServerBulkWriterOptions<T> _options;
    private readonly ISqlBulkCopyClientFactory _clientFactory;

    public SqlServerBulkWriter(
        IDbConnectionFactory connectionFactory,
        DatabaseOptions defaultOptions,
        SqlServerBulkWriterOptions<T> options,
        ISqlBulkCopyClientFactory? clientFactory = null)
        : this(
            () => connectionFactory.CreateConnection(options.OverrideOptions ?? defaultOptions),
            options,
            clientFactory)
    {
    }

    public SqlServerBulkWriter(
        Func<DbConnection> connectionFactory,
        SqlServerBulkWriterOptions<T> options,
        ISqlBulkCopyClientFactory? clientFactory = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _options = ValidateOptions(options);
        _clientFactory = clientFactory ?? new SqlBulkCopyClientFactory();
    }

    internal SqlServerBulkWriter(
        DbConnection connection,
        DbTransaction? transaction,
        SqlServerBulkWriterOptions<T> options,
        ISqlBulkCopyClientFactory clientFactory)
    {
        _sharedConnection = connection ?? throw new ArgumentNullException(nameof(connection));
        _sharedTransaction = transaction;
        _options = ValidateOptions(options);
        _clientFactory = clientFactory ?? new SqlBulkCopyClientFactory();
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
            await ExecuteOnConnectionAsync(_sharedConnection, rows, cancellationToken, ownsConnection: false).ConfigureAwait(false);
            return;
        }

        var connection = _connectionFactory!();
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteOnConnectionAsync(connection, rows, cancellationToken, ownsConnection: true).ConfigureAwait(false);
        }
    }

    private async Task ExecuteOnConnectionAsync(
        DbConnection connection,
        IEnumerable<T> rows,
        CancellationToken cancellationToken,
        bool ownsConnection)
    {
        var client = _clientFactory.Create(connection, _options.BulkCopyOptions, _sharedTransaction);
        var reader = new ObjectArrayDataReader<T>(_options.ColumnNames, rows, _options.ValueSelector!, _options.Columns);
        ConfigureClient(client);

        await using (client.ConfigureAwait(false))
        await using (reader.ConfigureAwait(false))
        {
            await client.WriteToServerAsync(reader, cancellationToken).ConfigureAwait(false);
        }

        if (!ownsConnection)
        {
            // Caller manages connection lifetime.
            await Task.CompletedTask;
        }
    }

    private void ConfigureClient(ISqlBulkCopyClient client)
    {
        client.DestinationTableName = _options.DestinationTable;
        if (_options.BatchSize is { } batchSize)
        {
            client.BatchSize = batchSize;
        }

        if (_options.BulkCopyTimeoutSeconds is { } timeoutSeconds)
        {
            client.BulkCopyTimeout = timeoutSeconds;
        }

        foreach (var column in _options.ColumnNames)
        {
            client.AddColumnMapping(column, column);
        }
    }

    private static SqlServerBulkWriterOptions<T> ValidateOptions(SqlServerBulkWriterOptions<T> options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.DestinationTable))
        {
            throw new ArgumentException("Destination table is required.", nameof(options));
        }

        if (options.ColumnNames.Count == 0)
        {
            throw new ArgumentException("At least one column name must be specified.", nameof(options));
        }

        if (options.ValueSelector is null)
        {
            throw new ArgumentException("Value selector must be provided.", nameof(options));
        }

        return options;
    }
}

internal sealed class ObjectArrayDataReader<T> : DbDataReader
{
    private readonly IReadOnlyList<string> _columns;
    private readonly IReadOnlyList<BulkColumn>? _columnMetadata;
    private readonly IEnumerator<T> _enumerator;
    private readonly Func<T, object?[]> _selector;
    private object?[]? _current;
    private bool _ownsBuffer;

    public ObjectArrayDataReader(
        IReadOnlyList<string> columns,
        IEnumerable<T> rows,
        Func<T, object?[]> selector,
        IReadOnlyList<BulkColumn>? columnMetadata = null)
    {
        _columns = columns ?? throw new ArgumentNullException(nameof(columns));
        _enumerator = rows.GetEnumerator();
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _columnMetadata = columnMetadata;
    }

    public override bool Read()
    {
        if (!_enumerator.MoveNext())
        {
            ReturnBuffer();
            return false;
        }

        ReturnBuffer();
        var projected = _selector(_enumerator.Current);
        if (projected.Length == _columns.Count)
        {
            _current = projected;
            _ownsBuffer = false;
        }
        else
        {
            _current = ArrayPool<object?>.Shared.Rent(_columns.Count);
            Array.Clear(_current, 0, _columns.Count);
            Array.Copy(projected, _current, Math.Min(projected.Length, _columns.Count));
            _ownsBuffer = true;
        }

        return true;
    }

    public override int FieldCount => _columns.Count;
    public override string GetName(int ordinal) => _columns[ordinal];
    public override object GetValue(int ordinal) => _current![ordinal]!;
    public override bool IsDBNull(int ordinal) => _current![ordinal] is null or DBNull;
    public override bool HasRows => true;
    public override bool IsClosed => false;
    public override int RecordsAffected => -1;
    public override int Depth => 0;
    public override bool NextResult() => false;
    public override void Close()
    {
        ReturnBuffer();
        base.Close();
    }
    protected override void Dispose(bool disposing)
    {
        ReturnBuffer();
        base.Dispose(disposing);
    }
    public override async ValueTask DisposeAsync()
    {
        ReturnBuffer();
        await base.DisposeAsync().ConfigureAwait(false);
    }
    public override string GetDataTypeName(int ordinal)
    {
        var meta = GetMetadata(ordinal);
        if (meta?.ProviderTypeName is { } providerType)
        {
            return providerType;
        }

        if (meta?.DbType is { } dbType)
        {
            return dbType.ToString();
        }

        return typeof(object).Name;
    }

    public override Type GetFieldType(int ordinal)
    {
        var meta = GetMetadata(ordinal);
        if (meta?.DbType is { } dbType)
        {
            return MapDbTypeToClr(dbType);
        }

        return typeof(object);
    }
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
    public override int GetOrdinal(string name)
    {
        for (var i = 0; i < _columns.Count; i++)
        {
            if (string.Equals(_columns[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        throw new ArgumentException($"Column '{name}' not found.", nameof(name));
    }

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public override char GetChar(int ordinal) => (char)GetValue(ordinal);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
    public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal)) ?? string.Empty;
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, _current!.Length);
        Array.Copy(_current, values, count);
        return count;
    }

    public override IEnumerator GetEnumerator() => (_current ?? Array.Empty<object?>()).GetEnumerator();

    private void ReturnBuffer()
    {
        if (_ownsBuffer && _current is not null)
        {
            ArrayPool<object?>.Shared.Return(_current, clearArray: true);
        }

        _current = null;
        _ownsBuffer = false;
    }

    private BulkColumn? GetMetadata(int ordinal)
    {
        if (_columnMetadata is null || ordinal < 0 || ordinal >= _columnMetadata.Count)
        {
            return null;
        }

        return _columnMetadata[ordinal];
    }

    private static Type MapDbTypeToClr(DbType dbType) =>
        dbType switch
        {
            DbType.AnsiString => typeof(string),
            DbType.String => typeof(string),
            DbType.AnsiStringFixedLength => typeof(string),
            DbType.StringFixedLength => typeof(string),
            DbType.Binary => typeof(byte[]),
            DbType.Boolean => typeof(bool),
            DbType.Byte => typeof(byte),
            DbType.Currency => typeof(decimal),
            DbType.Date => typeof(DateTime),
            DbType.DateTime => typeof(DateTime),
            DbType.DateTime2 => typeof(DateTime),
            DbType.DateTimeOffset => typeof(DateTimeOffset),
            DbType.Decimal => typeof(decimal),
            DbType.Double => typeof(double),
            DbType.Guid => typeof(Guid),
            DbType.Int16 => typeof(short),
            DbType.Int32 => typeof(int),
            DbType.Int64 => typeof(long),
            DbType.Object => typeof(object),
            DbType.SByte => typeof(sbyte),
            DbType.Single => typeof(float),
            DbType.Time => typeof(TimeSpan),
            DbType.UInt16 => typeof(ushort),
            DbType.UInt32 => typeof(uint),
            DbType.UInt64 => typeof(ulong),
            _ => typeof(object)
        };
}
