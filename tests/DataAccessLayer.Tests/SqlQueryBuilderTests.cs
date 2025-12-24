using System.Data;
using DataAccessLayer.Execution;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class SqlQueryBuilderTests
{
    [Fact]
    public void WhereEquals_BuildsParameterizedQuery()
    {
        var request = SqlQueryBuilder
            .SelectFrom("dbo.TodoItems", "Id", "Title")
            .WhereEquals("IsCompleted", false, DbType.Boolean)
            .OrderBy("CreatedUtc", descending: true)
            .WithTraceName("todo.pending")
            .Build();

        Assert.Equal("SELECT Id, Title FROM dbo.TodoItems WHERE IsCompleted = @p0 ORDER BY CreatedUtc DESC", request.CommandText);
        Assert.Equal("todo.pending", request.TraceName);
        var parameter = Assert.Single(request.Parameters);
        Assert.Equal("p0", parameter.Name);
        Assert.Equal(DbType.Boolean, parameter.DbType);
        Assert.False(parameter.TreatAsList);
    }

    [Fact]
    public void WhereIn_UsesListParameter()
    {
        var request = SqlQueryBuilder
            .SelectFrom("dbo.TodoItems")
            .WhereIn("Id", new object?[] { 1, 2, 3 }, DbType.Int32)
            .Build();

        Assert.Equal("SELECT * FROM dbo.TodoItems WHERE Id IN (@p0)", request.CommandText);
        var parameter = Assert.Single(request.Parameters);
        Assert.True(parameter.TreatAsList);
        Assert.Equal(3, parameter.Values?.Count);
    }
}
