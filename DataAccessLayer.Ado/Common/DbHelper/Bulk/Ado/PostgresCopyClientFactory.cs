using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Default factory that wraps <see cref="NpgsqlBinaryImporter"/>.
/// </summary>
public sealed class PostgresCopyClientFactory : IPostgresCopyClientFactory
{
    public IPostgresCopyClient Create(DbConnection connection, string copyCommand)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(copyCommand);

        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            throw new BulkOperationException("PostgreSQL bulk writer requires an NpgsqlConnection.");
        }

        var importer = npgsqlConnection.BeginBinaryImport(copyCommand);
        return new PostgresCopyClient(importer);
    }

    private sealed class PostgresCopyClient : IPostgresCopyClient
    {
        private readonly NpgsqlBinaryImporter _importer;
        private NpgsqlDbType?[]? _columnTypes;

        public PostgresCopyClient(NpgsqlBinaryImporter importer) => _importer = importer;

        public void ConfigureColumns(IReadOnlyList<BulkColumn>? columns)
        {
            if (columns is null || columns.Count == 0)
            {
                _columnTypes = null;
                return;
            }

            var resolved = new NpgsqlDbType?[columns.Count];
            for (var i = 0; i < columns.Count; i++)
            {
                resolved[i] = ResolveColumnType(columns[i]);
            }

            _columnTypes = resolved;
        }

        public void WriteRow(object?[] values)
        {
            _importer.StartRow();
            for (var i = 0; i < values.Length; i++)
            {
                WriteValue(values[i], GetColumnType(i));
            }
        }

        public async Task WriteRowAsync(object?[] values, CancellationToken cancellationToken = default)
        {
            await _importer.StartRowAsync(cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < values.Length; i++)
            {
                await WriteValueAsync(values[i], GetColumnType(i), cancellationToken).ConfigureAwait(false);
            }
        }

        public void Complete() => _importer.Complete();

        public async Task CompleteAsync(CancellationToken cancellationToken = default) =>
            await _importer.CompleteAsync(cancellationToken).ConfigureAwait(false);

        public ValueTask DisposeAsync() => _importer.DisposeAsync();

        public void Dispose() => _importer.Dispose();

        private NpgsqlDbType? GetColumnType(int ordinal)
        {
            if (_columnTypes is null || ordinal < 0 || ordinal >= _columnTypes.Length)
            {
                return null;
            }

            return _columnTypes[ordinal];
        }

        private NpgsqlDbType? ResolveColumnType(BulkColumn column)
        {
            if (!string.IsNullOrWhiteSpace(column.ProviderTypeName) &&
                TryMapProviderTypeName(column.ProviderTypeName, out var providerType))
            {
                return providerType;
            }

            if (column.DbType is { } dbType &&
                TryMapDbType(dbType, out var mappedType))
            {
                return mappedType;
            }

            return null;
        }

        private void WriteValue(object? value, NpgsqlDbType? type)
        {
            if (type is { } npgsqlType)
            {
                _importer.Write(value, npgsqlType);
            }
            else
            {
                _importer.Write(value);
            }
        }

        private Task WriteValueAsync(object? value, NpgsqlDbType? type, CancellationToken cancellationToken)
        {
            if (type is { } npgsqlType)
            {
                return _importer.WriteAsync(value, npgsqlType, cancellationToken);
            }

            return _importer.WriteAsync(value, cancellationToken);
        }

        private static bool TryMapProviderTypeName(string providerTypeName, out NpgsqlDbType npgsqlDbType)
        {
            if (Enum.TryParse(providerTypeName, ignoreCase: true, out npgsqlDbType))
            {
                return true;
            }

            if (ProviderTypeAliases.TryGetValue(providerTypeName, out npgsqlDbType))
            {
                return true;
            }

            // handle timestamptz style tokens
            if (providerTypeName.Equals("timestamptz", StringComparison.OrdinalIgnoreCase))
            {
                npgsqlDbType = NpgsqlDbType.TimestampTz;
                return true;
            }

            if (providerTypeName.Equals("timetz", StringComparison.OrdinalIgnoreCase))
            {
                npgsqlDbType = NpgsqlDbType.TimeTz;
                return true;
            }

            return false;
        }

        private static bool TryMapDbType(DbType dbType, out NpgsqlDbType npgsqlDbType)
        {
            npgsqlDbType = dbType switch
            {
                DbType.AnsiString => NpgsqlDbType.Text,
                DbType.String => NpgsqlDbType.Text,
                DbType.AnsiStringFixedLength => NpgsqlDbType.Char,
                DbType.StringFixedLength => NpgsqlDbType.Char,
                DbType.Binary => NpgsqlDbType.Bytea,
                DbType.Boolean => NpgsqlDbType.Boolean,
                DbType.Byte => NpgsqlDbType.Smallint,
                DbType.Currency => NpgsqlDbType.Money,
                DbType.Date => NpgsqlDbType.Date,
                DbType.DateTime => NpgsqlDbType.Timestamp,
                DbType.DateTime2 => NpgsqlDbType.Timestamp,
                DbType.DateTimeOffset => NpgsqlDbType.TimestampTz,
                DbType.Decimal => NpgsqlDbType.Numeric,
                DbType.Double => NpgsqlDbType.Double,
                DbType.Guid => NpgsqlDbType.Uuid,
                DbType.Int16 => NpgsqlDbType.Smallint,
                DbType.Int32 => NpgsqlDbType.Integer,
                DbType.Int64 => NpgsqlDbType.Bigint,
                DbType.Object => NpgsqlDbType.Jsonb,
                DbType.SByte => NpgsqlDbType.Smallint,
                DbType.Single => NpgsqlDbType.Real,
                DbType.Time => NpgsqlDbType.Time,
                DbType.UInt16 => NpgsqlDbType.Integer,
                DbType.UInt32 => NpgsqlDbType.Bigint,
                DbType.UInt64 => NpgsqlDbType.Numeric,
                DbType.VarNumeric => NpgsqlDbType.Numeric,
                _ => (NpgsqlDbType)(-1)
            };

            return npgsqlDbType != (NpgsqlDbType)(-1);
        }

        private static readonly Dictionary<string, NpgsqlDbType> ProviderTypeAliases =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["jsonb"] = NpgsqlDbType.Jsonb,
                ["json"] = NpgsqlDbType.Json,
                ["uuid"] = NpgsqlDbType.Uuid,
                ["citext"] = NpgsqlDbType.Citext,
                ["text"] = NpgsqlDbType.Text,
                ["varchar"] = NpgsqlDbType.Varchar,
                ["char"] = NpgsqlDbType.Char,
                ["bpchar"] = NpgsqlDbType.Char,
                ["int4"] = NpgsqlDbType.Integer,
                ["int8"] = NpgsqlDbType.Bigint,
                ["int2"] = NpgsqlDbType.Smallint,
                ["serial"] = NpgsqlDbType.Integer,
                ["bigserial"] = NpgsqlDbType.Bigint,
                ["numeric"] = NpgsqlDbType.Numeric,
                ["decimal"] = NpgsqlDbType.Numeric,
                ["bool"] = NpgsqlDbType.Boolean,
                ["bytea"] = NpgsqlDbType.Bytea,
                ["money"] = NpgsqlDbType.Money,
                ["timestamptz"] = NpgsqlDbType.TimestampTz,
                ["timestamp with time zone"] = NpgsqlDbType.TimestampTz,
                ["timestamp without time zone"] = NpgsqlDbType.Timestamp,
                ["date"] = NpgsqlDbType.Date,
                ["time"] = NpgsqlDbType.Time,
                ["timetz"] = NpgsqlDbType.TimeTz
            };
    }
}
