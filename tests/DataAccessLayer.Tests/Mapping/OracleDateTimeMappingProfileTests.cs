using System;
using System.Data;
using DataAccessLayer.Mapping;
using Xunit;

namespace DataAccessLayer.Tests.Mapping;

public sealed class OracleDateTimeMappingProfileTests
{
    private readonly OracleDateTimeMappingProfile _profile = new();

    [Fact]
    public void TryConvert_DateTime_ToDateTimeOffset()
    {
        var source = new DateTime(2024, 05, 01, 8, 30, 0, DateTimeKind.Unspecified);

        Assert.True(_profile.TryConvert("CreatedOn", nameof(TestEntity.CreatedAt), typeof(DateTimeOffset), source, out var result));
        var dto = Assert.IsType<DateTimeOffset>(result);
        Assert.Equal(DateTimeKind.Utc, dto.UtcDateTime.Kind);
        Assert.Equal(8, dto.UtcDateTime.Hour);
    }

    [Fact]
    public void TryConvert_String_ToDateTimeOffset()
    {
        Assert.True(_profile.TryConvert("CreatedOn", nameof(TestEntity.CreatedAt), typeof(DateTimeOffset), "2024-05-01T08:30:00Z", out var result));
        var dto = Assert.IsType<DateTimeOffset>(result);
        Assert.Equal(8, dto.UtcDateTime.Hour);
    }

    [Fact]
    public void ReflectionMapper_UsesProfile_ForDateTimeOffset()
    {
        var mapper = new ReflectionDataMapper<TestEntity>(profiles: new IMappingProfile[] { _profile });
        var table = new DataTable();
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Rows.Add(new DateTime(2024, 05, 01, 8, 30, 0, DateTimeKind.Unspecified));

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());

        var entity = mapper.Map(reader);
        Assert.Equal(DateTimeKind.Utc, entity.CreatedAt.UtcDateTime.Kind);
    }

    private sealed class TestEntity
    {
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.MinValue;
    }
}
