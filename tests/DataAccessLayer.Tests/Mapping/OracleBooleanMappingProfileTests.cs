using System;
using System.Data;
using DataAccessLayer.Mapping;
using Xunit;

namespace DataAccessLayer.Tests.Mapping;

public sealed class OracleBooleanMappingProfileTests
{
    private readonly OracleBooleanMappingProfile _profile = new();

    [Theory]
    [InlineData("Y", true)]
    [InlineData("N", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("TRUE", true)]
    [InlineData("FALSE", false)]
    public void TryConvert_StringValues(string input, bool expected)
    {
        Assert.True(_profile.TryConvert("Flag", nameof(TestEntity.IsPreferred), typeof(bool), input, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1L)]
    [InlineData(1.0d)]
    public void TryConvert_NumericTrue(object value)
    {
        Assert.True(_profile.TryConvert("Flag", nameof(TestEntity.IsPreferred), typeof(bool), value, out var result));
        Assert.True((bool)result!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0L)]
    [InlineData(0.0d)]
    public void TryConvert_NumericFalse(object value)
    {
        Assert.True(_profile.TryConvert("Flag", nameof(TestEntity.IsPreferred), typeof(bool), value, out var result));
        Assert.False((bool)result!);
    }

    [Fact]
    public void ReflectionMapper_UsesProfile()
    {
        var mapper = new ReflectionDataMapper<TestEntity>(profiles: new IMappingProfile[] { _profile });
        var table = new DataTable();
        table.Columns.Add("IsPreferred", typeof(string));
        table.Rows.Add("Y");
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var entity = mapper.Map(reader);
        Assert.True(entity.IsPreferred);
    }

    private sealed class TestEntity
    {
        public bool IsPreferred { get; set; } = false;
    }
}
