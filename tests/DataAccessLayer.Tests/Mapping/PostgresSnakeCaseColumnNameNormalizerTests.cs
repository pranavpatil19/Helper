using System.Data;
using DataAccessLayer.Configuration;
using DataAccessLayer.Mapping;
using Xunit;

namespace DataAccessLayer.Tests.Mapping;

public sealed class PostgresSnakeCaseColumnNameNormalizerTests
{
    [Fact]
    public void Normalize_ConvertsSnakeCaseToPascalCase()
    {
        var normalizer = new PostgresSnakeCaseColumnNameNormalizer();
        Assert.Equal("CreatedAt", normalizer.Normalize("created_at"));
        Assert.Equal("OrderId", normalizer.Normalize("order_id"));
        Assert.Equal("Abc", normalizer.Normalize("abc"));
    }

    [Fact]
    public void RowMapper_UsesNormalizer()
    {
        var factory = new RowMapperFactory(new DbHelperOptions(), columnNameNormalizer: new PostgresSnakeCaseColumnNameNormalizer());
        var mapper = factory.Create<PostgresEntity>();

        var table = new DataTable();
        table.Columns.Add("order_id", typeof(int));
        table.Columns.Add("created_at", typeof(string));
        table.Rows.Add(5, "2024-05-01T08:30:00Z");

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());

        var entity = mapper.Map(reader);
        Assert.Equal(5, entity.OrderId);
        Assert.Equal("2024-05-01T08:30:00Z", entity.CreatedAt);
    }

    private sealed class PostgresEntity
    {
        public int OrderId { get; set; } = 0;
        public string CreatedAt { get; set; } = string.Empty;
    }
}
