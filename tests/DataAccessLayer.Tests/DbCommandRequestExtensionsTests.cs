using System;
using System.Data;
using DataAccessLayer.Execution;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class DbCommandRequestExtensionsTests
{
    [Fact]
    public void AsStoredProcedure_UsesExistingName_WhenOverrideMissing()
    {
        var request = new DbCommandRequest
        {
            CommandText = "dbo.GetCustomers"
        };

        var storedProc = request.AsStoredProcedure();

        Assert.Equal(CommandType.StoredProcedure, storedProc.CommandType);
        Assert.Equal("dbo.GetCustomers", storedProc.CommandText);
    }

    [Fact]
    public void AsStoredProcedure_OverridesName_WhenProvided()
    {
        var request = new DbCommandRequest
        {
            CommandText = "dbo.GetCustomers"
        };

        var storedProc = request.AsStoredProcedure("dbo.GetCustomersByRegion");

        Assert.Equal("dbo.GetCustomersByRegion", storedProc.CommandText);
    }

    [Fact]
    public void AsStoredProcedure_Throws_WhenResultingNameIsEmpty()
    {
        var request = new DbCommandRequest { CommandText = string.Empty };
        Assert.Throws<ArgumentException>(() => request.AsStoredProcedure());
    }
}
