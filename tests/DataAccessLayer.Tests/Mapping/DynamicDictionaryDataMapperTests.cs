using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using DataAccessLayer.Mapping;
using Xunit;

namespace DataAccessLayer.Tests;

#nullable enable

public sealed class DynamicDictionaryDataMapperTests
{
    [Fact]
    public void DictionaryMapper_ProducesCaseInsensitivePayload()
    {
        var mapper = new DictionaryDataMapper();
        using var reader = new SingleRowReader(
            new[] { "Id", "Name" },
            new object?[] { 1, "Alice" });

        Assert.True(reader.Read());
        var result = mapper.Map(reader);

        Assert.Equal(1, result["Id"]);
        Assert.Equal("Alice", result["NAME"]);
    }

    [Fact]
    public void DynamicMapper_ExposesProperties()
    {
        var mapper = new DynamicDataMapper();
        using var reader = new SingleRowReader(
            new[] { "Total", "CreatedOn" },
            new object?[] { 99.5m, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        Assert.True(reader.Read());
        dynamic payload = mapper.Map(reader);

        Assert.Equal(99.5m, payload.Total);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), payload.CreatedOn);
    }

    private sealed class SingleRowReader : DbDataReader
    {
        private readonly string[] _columns;
        private readonly object?[] _values;
        private bool _read;

        public SingleRowReader(string[] columns, object?[] values)
        {
            _columns = columns;
            _values = values;
        }

        public override bool Read()
        {
            if (_read)
            {
                return false;
            }

            _read = true;
            return true;
        }

        public override int FieldCount => _columns.Length;
        public override string GetName(int ordinal) => _columns[ordinal];
        public override object GetValue(int ordinal) => _values[ordinal]!;
        public override bool IsDBNull(int ordinal) => _values[ordinal] is null or DBNull;
        public override bool HasRows => true;
        public override int RecordsAffected => 0;
        public override bool IsClosed => false;
        public override int Depth => 0;
        public override bool NextResult() => false;
        public override object this[int ordinal] => GetValue(ordinal)!;
        public override object this[string name] => GetValue(GetOrdinal(name))!;
        public override int GetOrdinal(string name) => Array.FindIndex(_columns, c => c.Equals(name, StringComparison.OrdinalIgnoreCase));
        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
        public override Type GetFieldType(int ordinal) => _values[ordinal]?.GetType() ?? typeof(object);
        public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
        public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal)!;
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
            var count = Math.Min(values.Length, _values.Length);
            Array.Copy(_values, values, count);
            return count;
        }
        public override IEnumerator GetEnumerator() => ((IEnumerable)_values).GetEnumerator();
    }
}
