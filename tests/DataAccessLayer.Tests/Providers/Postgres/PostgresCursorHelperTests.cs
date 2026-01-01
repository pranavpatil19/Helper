using System;
using System.Data;
using DataAccessLayer.Execution;
using DalParameter = DataAccessLayer.Execution.Builders.DbParameter;
using DataAccessLayer.Providers.Postgres;
using Xunit;

namespace DataAccessLayer.Tests.Providers.Postgres;

public sealed class PostgresCursorHelperTests
{
    [Fact]
    public void BuildMultiCursorRequest_ComposesDoBlockAndFetches()
    {
        var request = PostgresCursorHelper.BuildMultiCursorRequest(
            new[]
            {
                "SELECT * FROM orders WHERE status = @status",
                "SELECT * FROM invoices"
            },
            new[]
            {
                DalParameter.Input("status", "Open")
            },
            traceName: "cursor.fetch");

        Assert.Equal(CommandType.Text, request.CommandType);
        Assert.Equal("cursor.fetch", request.TraceName);
        Assert.Single(request.Parameters);
        Assert.Contains("DO $$", request.CommandText);
        Assert.Contains("OPEN cursor_0 FOR SELECT * FROM orders WHERE status = @status;", request.CommandText);
        Assert.Contains("FETCH ALL FROM cursor_1;", request.CommandText);
    }

    [Fact]
    public void BuildMultiCursorRequest_Throws_WhenNoStatements()
    {
        Assert.Throws<ArgumentException>(() => PostgresCursorHelper.BuildMultiCursorRequest(Array.Empty<string>()));
    }
}
