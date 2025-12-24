#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using DataAccessLayer.Mapping;
using DataAccessLayer.Mapping.GeneratedSamples;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class MapperStrategyTests
{
    [Theory]
    [InlineData(MapperStrategy.Reflection)]
    [InlineData(MapperStrategy.IlEmit)]
    public void EntityMappers_ProjectRows(MapperStrategy strategy)
    {
        var mapper = DataMapperFactory.CreateMapper<DummyEntity>(strategy);
        using var reader = new SingleRowReader(new[] { "Id", "Name" }, new object?[] { 10, "entity" });

        Assert.True(reader.Read());
        var entity = mapper.Map(reader);

        Assert.Equal(10, entity.Id);
        Assert.Equal("entity", entity.Name);
    }

    [Fact]
    public void GeneratedMapper_ProjectsRows()
    {
        var mapper = DataMapperFactory.CreateMapper<SampleGeneratedEntity>(MapperStrategy.SourceGenerator);
        using var reader = new SingleRowReader(new[] { "Id", "Name" }, new object?[] { 7, "gen" });

        Assert.True(reader.Read());
        var entity = mapper.Map(reader);

        Assert.Equal((7, "gen"), (entity.Id, entity.Name));
    }

    [Fact]
    public void DictionaryMapper_IsCaseInsensitive()
    {
        var mapper = DataMapperFactory.CreateMapper<IReadOnlyDictionary<string, object?>>(
            MapperStrategy.Dictionary,
            ignoreCase: true);
        using var reader = new SingleRowReader(new[] { "Value" }, new object?[] { 123 });

        Assert.True(reader.Read());
        var dict = mapper.Map(reader);

        Assert.Equal(123, dict["value"]);
    }

    [Fact]
    public void DynamicMapper_ExposesMembers()
    {
        var mapper = DataMapperFactory.CreateMapper<object>(MapperStrategy.Dynamic);
        using var reader = new SingleRowReader(new[] { "Amount" }, new object?[] { 42m });

        Assert.True(reader.Read());
        dynamic dynamicRow = mapper.Map(reader);

        Assert.Equal(42m, dynamicRow.Amount);
    }

    private sealed class DummyEntity
    {
        public int Id { get; set; } = 0;
        public string? Name { get; set; } = string.Empty;
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
        public override int GetOrdinal(string name)
        {
            for (var i = 0; i < _columns.Length; i++)
            {
                if (string.Equals(_columns[i], name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        public override object GetValue(int ordinal) => _values[ordinal]!;
        public override bool IsDBNull(int ordinal) => _values[ordinal] is null;
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));
        public override int Depth => 0;
        public override bool HasRows => true;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override bool NextResult() => false;
        public override string GetDataTypeName(int ordinal) => _values[ordinal]?.GetType().Name ?? typeof(object).Name;
        public override System.Type GetFieldType(int ordinal) => _values[ordinal]?.GetType() ?? typeof(object);
        public override IEnumerator GetEnumerator() => ((IEnumerable)_values).GetEnumerator();
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
            var count = Math.Min(values.Length, _values.Length);
            Array.Copy(_values, values, count);
            return count;
        }
    }
}
