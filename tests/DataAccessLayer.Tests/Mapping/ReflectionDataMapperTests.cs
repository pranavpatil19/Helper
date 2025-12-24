using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Configuration;
using DataAccessLayer.Mapping;
using DataAccessLayer.Exceptions;
using Xunit;

namespace DataAccessLayer.Tests;

#nullable enable

public sealed class ReflectionDataMapperTests
{
    [Fact]
    public void MapsSimpleProperties()
    {
        var mapper = new ReflectionDataMapper<DummyEntity>();
        using var reader = new DummyReader(new[] { "Id", "Name", "Amount" }, new object[] { 7, "test", 42.5m });

        Assert.True(reader.Read());
        var entity = mapper.Map(reader);

        Assert.Equal(7, entity.Id);
        Assert.Equal("test", entity.Name);
        Assert.Equal(42.5m, entity.Amount);
    }

    [Fact]
    public async Task ReflectionReaderMapper_MapsAllRowsAsync()
    {
        var mapper = new ReflectionReaderMapper<DummyEntity>();
        await using var reader = new DummyAsyncReader(
            new[] { "Id", "Name" },
            new List<object[]>
            {
                new object[] { 1, "A" },
                new object[] { 2, "B" }
            });

        var results = await mapper.MapAllAsync(reader);
        Assert.Collection(results,
            item => Assert.Equal((1, "A"), (item.Id, item.Name)),
            item => Assert.Equal((2, "B"), (item.Id, item.Name)));
    }

    [Fact]
    public void IlEmitDataMapper_MapsProperties()
    {
        var mapper = new IlEmitDataMapper<DummyEntity>();
        using var reader = new DummyReader(new[] { "Id", "Name", "Amount" }, new object[] { 99, "fast", 10m });
        Assert.True(reader.Read());
        var entity = mapper.Map(reader);
        Assert.Equal((99, "fast", 10m), (entity.Id, entity.Name, entity.Amount));
    }

    [Fact]
    public void DataMapperFactory_ReturnsRequestedStrategy()
    {
        var mapper = DataMapperFactory.CreateMapper<DummyEntity>(MapperStrategy.IlEmit);
        Assert.IsType<IlEmitDataMapper<DummyEntity>>(mapper);
    }

    [Fact]
    public void ColumnMap_AllowsAliasBinding()
    {
        var columnMap = new Dictionary<string, string>
        {
            ["Name"] = "full_name",
            ["Amount"] = "total_amount"
        };

        var mapper = DataMapperFactory.CreateMapper<DummyEntity>(
            MapperStrategy.Reflection,
            propertyToColumnMap: columnMap);

        using var reader = new DummyReader(
            new[] { "Id", "full_name", "total_amount" },
            new object[] { 11, "alias", 99m });

        Assert.True(reader.Read());
        var entity = mapper.Map(reader);

        Assert.Equal((11, "alias", 99m), (entity.Id, entity.Name, entity.Amount));
    }

    [Fact]
    public void SourceGeneratedMapper_MapsRows()
    {
        var mapper = DataMapperFactory.CreateMapper<DataAccessLayer.Mapping.GeneratedSamples.SampleGeneratedEntity>(MapperStrategy.SourceGenerator);
        using var reader = new DummyReader(new[] { "Id", "Name" }, new object[] { 5, "gen" });
        Assert.True(reader.Read());
        var entity = mapper.Map(reader);
        Assert.Equal((5, "gen"), (entity.Id, entity.Name));
    }

    [Fact]
    public void DataMapperFactory_CreatesDictionaryMapper()
    {
        var mapper = DataMapperFactory.CreateMapper<IReadOnlyDictionary<string, object?>>(
            MapperStrategy.Dictionary);
        using var reader = new DummyReader(new[] { "Key" }, new object[] { "value" });

        Assert.True(reader.Read());
        var result = mapper.Map(reader);

        Assert.Equal("value", result["Key"]);
    }

    [Fact]
    public void DataMapperFactory_CreatesDynamicMapper()
    {
        var mapper = DataMapperFactory.CreateMapper<object>(MapperStrategy.Dynamic);
        using var reader = new DummyReader(new[] { "Amount" }, new object[] { 5 });

        Assert.True(reader.Read());
        dynamic result = mapper.Map(reader);

        Assert.Equal(5, result.Amount);
    }

    [Fact]
    public void ReflectionDataMapper_ConvertsProviderSpecificValues()
    {
        var mapper = new ReflectionDataMapper<OracleLikeEntity>();
        using var reader = new DummyReader(
            new[] { "IsPreferred", "CreatedOn" },
            new object[] { 1m, "2024-05-20T12:30:00Z" });

        Assert.True(reader.Read());
        var entity = mapper.Map(reader);

        Assert.True(entity.IsPreferred);
        var expectedCreatedOn = DateTime.Parse(
                "2024-05-20T12:30:00Z",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
            .ToLocalTime();
        Assert.Equal(expectedCreatedOn, entity.CreatedOn);
    }

    [Fact]
    public void MappingProfile_OverridesConversion()
    {
        var profile = new BooleanStringMappingProfile();
        var mapper = new ReflectionDataMapper<OracleLikeEntity>(profiles: new[] { profile });
        using var reader = new DummyReader(new[] { "IsPreferred" }, new object[] { "Y" });

        Assert.True(reader.Read());
        var entity = mapper.Map(reader);

        Assert.True(entity.IsPreferred);
    }

    [Fact]
    public void RowMapperFactory_InjectsProfiles()
    {
        var profile = new BooleanStringMappingProfile();
        var factory = new RowMapperFactory(new DbHelperOptions(), new[] { profile });
        var mapper = factory.Create<OracleLikeEntity>();

        using var reader = new DummyReader(new[] { "IsPreferred" }, new object[] { "N" });
        Assert.True(reader.Read());

        var entity = mapper.Map(reader);
        Assert.False(entity.IsPreferred);
    }

    [Fact]
    public void DataMapperFactory_InvalidCombination_Throws()
    {
        Assert.Throws<RowMappingException>(() =>
            DataMapperFactory.CreateMapper<DummyEntity>(MapperStrategy.Dictionary));
    }

    private sealed class DummyEntity
    {
        public int Id { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; } = 0m;
    }

    private sealed class OracleLikeEntity
    {
        public bool IsPreferred { get; set; } = false;
        public DateTime CreatedOn { get; set; } = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
    }

    private sealed class BooleanStringMappingProfile : IMappingProfile
    {
        public bool TryConvert(string columnName, string propertyName, Type targetType, object? sourceValue, out object? destinationValue)
        {
            if (targetType == typeof(bool) && sourceValue is string s && (s.Equals("Y", StringComparison.OrdinalIgnoreCase) || s.Equals("N", StringComparison.OrdinalIgnoreCase)))
            {
                destinationValue = s.Equals("Y", StringComparison.OrdinalIgnoreCase);
                return true;
            }

            destinationValue = null;
            return false;
        }
    }

    private sealed class DummyReader : DbDataReader
    {
        private readonly string[] _columns;
        private readonly object[] _values;
        private int _readCount;

        public DummyReader(string[] columns, object[] values)
        {
            _columns = columns;
            _values = values;
        }

        public override bool Read()
        {
            if (_readCount > 0)
            {
                return false;
            }

            _readCount++;
            return true;
        }

        public override int FieldCount => _columns.Length;
        public override string GetName(int ordinal) => _columns[ordinal];
        public override object GetValue(int ordinal) => _values[ordinal];
        public override bool IsDBNull(int ordinal) => _values[ordinal] is null or DBNull;
        public override bool HasRows => true;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;
        public override bool NextResult() => false;
        public override string GetDataTypeName(int ordinal) => _values[ordinal]?.GetType().Name ?? nameof(Object);
        public override Type GetFieldType(int ordinal) => _values[ordinal]?.GetType() ?? typeof(object);
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(Array.IndexOf(_columns, name));
        public override IEnumerator GetEnumerator() => ((IEnumerable)_values).GetEnumerator();
        public override int GetOrdinal(string name) => Array.FindIndex(_columns, column => column.Equals(name, StringComparison.OrdinalIgnoreCase));
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

    private sealed class DummyAsyncReader : DbDataReader
    {
        private readonly string[] _columns;
        private readonly List<object[]> _rows;
        private int _index = -1;

        public DummyAsyncReader(string[] columns, List<object[]> rows)
        {
            _columns = columns;
            _rows = rows;
        }

        public override bool Read()
        {
            _index++;
            return _index < _rows.Count;
        }

        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());

        public override int FieldCount => _columns.Length;
        public override string GetName(int ordinal) => _columns[ordinal];
        public override object GetValue(int ordinal) => _rows[_index][ordinal];
        public override bool IsDBNull(int ordinal) => _rows[_index][ordinal] is null or DBNull;
        public override bool HasRows => _rows.Count > 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;
        public override bool NextResult() => false;
        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
        public override Type GetFieldType(int ordinal) => _rows[_index][ordinal].GetType();
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(Array.IndexOf(_columns, name));
        public override IEnumerator GetEnumerator() => ((IEnumerable)_rows).GetEnumerator();
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
            var row = _rows[_index];
            var count = Math.Min(values.Length, row.Length);
            Array.Copy(row, values, count);
            return count;
        }
        public override int GetOrdinal(string name) => Array.FindIndex(_columns, column => column.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
