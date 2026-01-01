using System.Data;
using DataAccessLayer.Execution;
using Xunit;

namespace DataAccessLayer.Tests.Sql;

public sealed class SqlQueryBuilderTests
{
    [Fact]
    public void WhereEquals_BuildsParameterizedQuery()
    {
        var request = SqlQueryBuilder
            .SelectFrom("dbo.SampleRecords", "Id", "Name")
            .WhereEquals("IsActive", true, DbType.Boolean)
            .OrderBy("CreatedUtc", descending: true)
            .WithTraceName("records.active")
            .Build();

        Assert.Equal("SELECT Id, Name FROM dbo.SampleRecords WHERE IsActive = @p0 ORDER BY CreatedUtc DESC", request.CommandText);
        Assert.Equal("records.active", request.TraceName);
        var parameter = Assert.Single(request.Parameters);
        Assert.Equal("p0", parameter.Name);
        Assert.Equal(DbType.Boolean, parameter.DbType);
        Assert.False(parameter.TreatAsList);
    }

    [Fact]
    public void WhereIn_UsesListParameter()
    {
        var request = SqlQueryBuilder
            .SelectFrom("dbo.SampleRecords")
            .WhereIn("Id", new object?[] { 1, 2, 3 }, DbType.Int32)
            .Build();

        Assert.Equal("SELECT * FROM dbo.SampleRecords WHERE Id IN (@p0)", request.CommandText);
        var parameter = Assert.Single(request.Parameters);
        Assert.True(parameter.TreatAsList);
        Assert.Equal(3, parameter.Values?.Count);
    }
}
