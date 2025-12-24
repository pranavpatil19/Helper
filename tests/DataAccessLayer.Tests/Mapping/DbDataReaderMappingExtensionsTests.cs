using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using DataAccessLayer.Configuration;
using DataAccessLayer.Mapping;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class DbDataReaderMappingExtensionsTests
{
    private readonly RowMapperFactory _factory = new(new DbHelperOptions());

    [Fact]
    public void MapRows_ToClassList_UsesRowMapperFactory()
    {
        using var reader = CreateReader();

        var result = reader.MapRows<TestRecord>(_factory);

        var materialized = Assert.IsType<TestRecord[]>(result);
        Assert.Collection(
            materialized,
            first =>
            {
                Assert.Equal(1, first.Id);
                Assert.Equal("alpha", first.Name);
            },
            second =>
            {
                Assert.Equal(2, second.Id);
                Assert.Equal("beta", second.Name);
            });
    }

    [Fact]
    public void MapDictionaries_ReturnsCaseInsensitiveRows()
    {
        using var reader = CreateReader();

        var dictionaries = reader.MapDictionaries(_factory);

        Assert.Collection(
            dictionaries,
            first => Assert.Equal("alpha", first["Name"]),
            second => Assert.Equal("beta", second["Name"]));
    }

    [Fact]
    public void ToDataTable_CopiesSchema()
    {
        using var reader = CreateReader();

        var table = reader.ToDataTable("Copy");

        Assert.Equal("Copy", table.TableName);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(2, table.Columns.Count);
        Assert.Equal("alpha", table.Rows[0]["Name"]);
    }

    private static DbDataReader CreateReader()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add(1, "alpha");
        table.Rows.Add(2, "beta");

        return table.CreateDataReader();
    }

    private sealed class TestRecord
    {
        public int Id { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
    }
}
