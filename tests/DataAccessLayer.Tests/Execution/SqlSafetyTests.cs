using System;
using DataAccessLayer.Execution;
using Xunit;

namespace DataAccessLayer.Tests.Execution;

public sealed class SqlSafetyTests
{
    [Fact]
    public void EnsureClause_TrimsAndReturnsFragment()
    {
        var fragment = SqlSafety.EnsureClause("  dbo.Users  ", "fragment");
        Assert.Equal("dbo.Users", fragment);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Users; DROP TABLE")]
    [InlineData("Users -- comment")]
    [InlineData("Users /* comment */")]
    public void EnsureClause_ThrowsOnForbiddenTokens(string input)
    {
        Assert.Throws<ArgumentException>(() => SqlSafety.EnsureClause(input, nameof(input)));
    }
}
