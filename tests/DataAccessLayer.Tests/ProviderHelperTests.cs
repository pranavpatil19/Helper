using System.Collections.Generic;
using System.Data;
using DataAccessLayer.Execution;
using DataAccessLayer.Providers.Oracle;
using DataAccessLayer.Providers.Postgres;
using DataAccessLayer.Providers.SqlServer;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class ProviderHelperTests
{
    [Fact]
    public void PostgresJsonb_SerializesValue()
    {
        var parameter = PostgresParameterHelper.Jsonb("payload", new { Name = "John" });
        Assert.Equal("jsonb", parameter.ProviderTypeName);
        Assert.Equal(DbType.String, parameter.DbType);
        Assert.Contains("John", (string)parameter.Value!);
    }

    [Fact]
    public void PostgresArray_SetsList()
    {
        var parameter = PostgresParameterHelper.Array("ids", new[] { 1, 2, 3 }, "_int4");
        Assert.True(parameter.TreatAsList);
        Assert.Equal("_int4", parameter.ProviderTypeName);
        Assert.Equal(3, parameter.Values!.Count);
    }

    [Fact]
    public void SqlServerTvpBuilder_CreatesDataTable()
    {
        var rows = new[]
        {
            new Person(1, "Alice"),
            new Person(2, "Bob")
        };

        var table = SqlServerTvpBuilder.ToDataTable(rows, x => x.Id, x => x.Name);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(2, table.Columns.Count);
    }

    [Fact]
    public void SqlServerTvpBuilder_CreatesParameter()
    {
        var rows = new[] { new Person(1, "Alice") };
        var parameter = SqlServerTvpBuilder.CreateParameter("items", "dbo.ItemType", rows, x => x.Id, x => x.Name);
        Assert.Equal("items", parameter.Name);
        Assert.Equal("dbo.ItemType", parameter.ProviderTypeName);
        var table = Assert.IsType<DataTable>(parameter.Value);
        Assert.Single(table.Rows);
    }

    [Fact]
    public void OracleRefCursorParameter_IsConfigured()
    {
        var parameter = OracleParameterHelper.RefCursor("cursor");
        Assert.Equal(ParameterDirection.Output, parameter.Direction);
        Assert.Equal(DbType.Object, parameter.DbType);
        Assert.Equal("RefCursor", parameter.ProviderTypeName);
    }

    [Fact]
    public void OracleArrayParameter_UsesStructuredBuilder()
    {
        var parameter = OracleParameterHelper.Array("ids", new[] { 1, 2 }, DbType.Int32);
        Assert.True(parameter.TreatAsList);
        Assert.Equal(DbType.Int32, parameter.DbType);
    }

    private sealed record Person(int Id, string Name);
}
