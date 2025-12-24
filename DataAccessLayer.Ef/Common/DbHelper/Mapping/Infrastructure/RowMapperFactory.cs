using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DataAccessLayer.Configuration;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Default implementation that bridges <see cref="IDataMapper{T}"/> strategies to <see cref="IRowMapper{T}"/>.
/// </summary>
public sealed class RowMapperFactory : IRowMapperFactory
{
    private readonly DbHelperOptions _options;
    private readonly ConcurrentDictionary<RowMapperCacheKey, object> _cache = new();
    private readonly IMappingProfile[] _profiles;
    private readonly IColumnNameNormalizer? _columnNameNormalizer;

    public RowMapperFactory(
        DbHelperOptions options,
        IEnumerable<IMappingProfile>? profiles = null,
        IColumnNameNormalizer? columnNameNormalizer = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _profiles = profiles?.ToArray() ?? Array.Empty<IMappingProfile>();
        _columnNameNormalizer = columnNameNormalizer;
    }

    public IRowMapper<T> Create<T>(RowMapperRequest? request = null) where T : class
    {
        var resolved = ResolveRequest(request);

        if (_options.EnableMapperCaching && resolved.PropertyToColumnMap is null)
        {
            var key = new RowMapperCacheKey(typeof(T), resolved.Strategy, resolved.IgnoreCase);
            if (_cache.TryGetValue(key, out var cached))
            {
                return (IRowMapper<T>)cached;
            }

            var mapper = CreateMapper<T>(resolved);
            return (IRowMapper<T>)_cache.GetOrAdd(key, mapper);
        }

        return CreateMapper<T>(resolved);
    }

    private IRowMapper<T> CreateMapper<T>(ResolvedRowMapperRequest resolved)
        where T : class
    {
        var dataMapper = DataMapperFactory.CreateMapper<T>(
            resolved.Strategy,
            resolved.IgnoreCase,
            resolved.PropertyToColumnMap,
            _profiles);
        return new DataMapperRowMapper<T>(dataMapper, _columnNameNormalizer);
    }

    private ResolvedRowMapperRequest ResolveRequest(RowMapperRequest? request)
    {
        return new ResolvedRowMapperRequest(
            request?.Strategy ?? _options.DefaultMapperStrategy,
            request?.IgnoreCase ?? _options.IgnoreCase,
            request?.PropertyToColumnMap);
    }

    private readonly record struct ResolvedRowMapperRequest(
        MapperStrategy Strategy,
        bool IgnoreCase,
        System.Collections.Generic.IReadOnlyDictionary<string, string>? PropertyToColumnMap);

    private readonly record struct RowMapperCacheKey(Type Type, MapperStrategy Strategy, bool IgnoreCase);

    private sealed class DataMapperRowMapper<T> : IRowMapper<T>
        where T : class
    {
        private readonly IDataMapper<T> _mapper;
        private readonly IColumnNameNormalizer? _normalizer;

        public DataMapperRowMapper(IDataMapper<T> mapper, IColumnNameNormalizer? normalizer)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _normalizer = normalizer;
        }

        public T Map(System.Data.Common.DbDataReader reader)
        {
            if (_normalizer is null)
            {
                return _mapper.Map(reader);
            }

            return reader is NormalizingDbDataReader normalizingReader
                ? _mapper.Map(normalizingReader)
                : _mapper.Map(new NormalizingDbDataReader(reader, _normalizer));
        }
    }

    private sealed class NormalizingDbDataReader : System.Data.Common.DbDataReader
    {
        private readonly System.Data.Common.DbDataReader _inner;
        private readonly IColumnNameNormalizer _normalizer;

        public NormalizingDbDataReader(System.Data.Common.DbDataReader inner, IColumnNameNormalizer normalizer)
        {
            _inner = inner;
            _normalizer = normalizer;
        }

        public override string GetName(int ordinal)
        {
            var original = _inner.GetName(ordinal);
            return _normalizer.Normalize(original);
        }

        #region DbDataReader passthrough
        public override object this[int ordinal] => _inner[ordinal];
        public override object this[string name] => _inner[name];
        public override int Depth => _inner.Depth;
        public override int FieldCount => _inner.FieldCount;
        public override int VisibleFieldCount => _inner.VisibleFieldCount;
        public override bool HasRows => _inner.HasRows;
        public override bool IsClosed => _inner.IsClosed;
        public override int RecordsAffected => _inner.RecordsAffected;
        public override bool GetBoolean(int ordinal) => _inner.GetBoolean(ordinal);
        public override byte GetByte(int ordinal) => _inner.GetByte(ordinal);
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => _inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
        public override char GetChar(int ordinal) => _inner.GetChar(ordinal);
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => _inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
        public override string GetDataTypeName(int ordinal) => _inner.GetDataTypeName(ordinal);
        public override DateTime GetDateTime(int ordinal) => _inner.GetDateTime(ordinal);
        public override decimal GetDecimal(int ordinal) => _inner.GetDecimal(ordinal);
        public override double GetDouble(int ordinal) => _inner.GetDouble(ordinal);
        public override System.Collections.IEnumerator GetEnumerator() => ((System.Collections.IEnumerable)_inner).GetEnumerator();
        public override Type GetFieldType(int ordinal) => _inner.GetFieldType(ordinal);
        public override float GetFloat(int ordinal) => _inner.GetFloat(ordinal);
        public override Guid GetGuid(int ordinal) => _inner.GetGuid(ordinal);
        public override short GetInt16(int ordinal) => _inner.GetInt16(ordinal);
        public override int GetInt32(int ordinal) => _inner.GetInt32(ordinal);
        public override long GetInt64(int ordinal) => _inner.GetInt64(ordinal);
        public override int GetOrdinal(string name) => _inner.GetOrdinal(name);
        public override string GetString(int ordinal) => _inner.GetString(ordinal);
        public override object GetValue(int ordinal) => _inner.GetValue(ordinal);
        public override int GetValues(object[] values) => _inner.GetValues(values);
        public override bool IsDBNull(int ordinal) => _inner.IsDBNull(ordinal);
        public override bool NextResult() => _inner.NextResult();
        public override bool Read() => _inner.Read();
        public override void Close() => _inner.Close();
        public override System.Data.DataTable? GetSchemaTable() => _inner.GetSchemaTable();
        #endregion
    }
}
