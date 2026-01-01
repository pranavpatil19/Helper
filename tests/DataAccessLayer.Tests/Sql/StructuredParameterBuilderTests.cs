using System.Collections.Generic;
using System.Data;
using DataAccessLayer.Execution;
using Xunit;

namespace DataAccessLayer.Tests.Sql;

#nullable enable

public sealed class StructuredParameterBuilderTests
{
    [Fact]
    public void SqlServerTableValuedParameter_PopulatesProviderTypeName()
    {
        var param = StructuredParameterBuilder.SqlServerTableValuedParameter("Items", new object(), "dbo.ItemList");
        Assert.Equal("Items", param.Name);
        Assert.Equal("dbo.ItemList", param.ProviderTypeName);
        Assert.Equal(DbType.Object, param.DbType);
    }

    [Fact]
    public void PostgresArray_MaterializesValues()
    {
        var param = StructuredParameterBuilder.PostgresArray("ids", new[] { 1, 2, 3 }, DbType.Int32, "_int4");
        Assert.True(param.TreatAsList);
        Assert.Equal("_int4", param.ProviderTypeName);
        Assert.Equal(DbType.Int32, param.DbType);
        Assert.Equal(new object?[] { 1, 2, 3 }, param.Values);
    }

    [Fact]
    public void OracleArray_BindsValues()
    {
        var param = StructuredParameterBuilder.OracleArray("names", new List<string> { "a", "b" }, DbType.String, 50);
        Assert.True(param.TreatAsList);
        Assert.Equal(DbType.String, param.DbType);
        Assert.Equal(50, param.Size);
        Assert.Equal("ArrayBind", param.ProviderTypeName);
    }
}
