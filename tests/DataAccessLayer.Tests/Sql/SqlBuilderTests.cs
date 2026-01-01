using System;
using System.Linq;
using DataAccessLayer.Execution;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests.Sql;

public sealed class SqlBuilderTests
{
    [Fact]
    public void Build_CreatesParameterizedWhereClause()
    {
        var builder = SqlBuilder.Select("u.Id", "u.Email")
            .From("dbo.Users u")
            .Where($"u.Status = {UserStatus.Active}")
            .Where($"u.CreatedOn >= {new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)}")
            .OrderBy("u.Id DESC");

        var result = builder.Build(DatabaseProvider.SqlServer);

        Assert.Equal(
            "SELECT u.Id, u.Email FROM dbo.Users u WHERE u.Status = @p0 AND u.CreatedOn >= @p1 ORDER BY u.Id DESC",
            result.CommandText);
        Assert.Equal(2, result.Parameters.Count);
        Assert.Equal("p0", result.Parameters[0].Name);
        Assert.Equal(UserStatus.Active, result.Parameters[0].Value);
    }

    [Fact]
    public void Build_ListParameter_ForPostgresUsesArray()
    {
        var ids = new[] { 1, 2, 3 };
        var builder = SqlBuilder.Select("*")
            .From("public.orders o")
            .Where($"o.id = ANY ({ids})");

        var result = builder.Build(DatabaseProvider.PostgreSql);

        Assert.Equal("SELECT * FROM public.orders o WHERE o.id = ANY (@p0)", result.CommandText);
        var parameter = Assert.Single(result.Parameters);
        Assert.True(parameter.TreatAsList);
        Assert.Equal(ids, parameter.Values?.OfType<int>());
    }

    [Fact]
    public void Paginate_SqlServerUsesOffsetFetch()
    {
        var builder = SqlBuilder.Select("*")
            .From("dbo.Events")
            .Paginate(pageNumber: 2, pageSize: 25);

        var result = builder.Build(DatabaseProvider.SqlServer);

        Assert.Equal("SELECT * FROM dbo.Events ORDER BY (SELECT 1) OFFSET 25 ROWS FETCH NEXT 25 ROWS ONLY", result.CommandText);
    }

    [Fact]
    public void Paginate_PostgresUsesLimitOffset()
    {
        var builder = SqlBuilder.Select("*")
            .From("public.logs")
            .Limit(10)
            .Offset(5);

        var result = builder.Build(DatabaseProvider.PostgreSql);

        Assert.Equal("SELECT * FROM public.logs LIMIT 10 OFFSET 5", result.CommandText);
    }

    [Fact]
    public void WhereIf_SkipsWhenConditionFalse()
    {
        var builder = SqlBuilder.Select("*")
            .From("dbo.Customers")
            .Where(false, $"Region = {"US"}");

        var result = builder.Build(DatabaseProvider.SqlServer);

        Assert.Equal("SELECT * FROM dbo.Customers", result.CommandText);
        Assert.Empty(result.Parameters);
    }

    [Fact]
    public void OrderBy_RejectsUnsafeFragments()
    {
        var builder = SqlBuilder.Select("*").From("dbo.Users");
        Assert.Throws<ArgumentException>(() => builder.OrderBy("-- invalid"));
    }

    private enum UserStatus
    {
        Inactive = 0,
        Active = 1
    }
}
