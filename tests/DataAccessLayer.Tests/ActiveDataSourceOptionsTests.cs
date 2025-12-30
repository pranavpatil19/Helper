using DataAccessLayer.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class ActiveDataSourceOptionsTests
{
    [Fact]
    public void ToDatabaseOptions_UsesDirectConnectionString_WhenProvided()
    {
        var active = new ActiveDataSourceOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=.;Database=Helper;"
        };

        var databaseOptions = active.ToDatabaseOptions();

        Assert.Equal(DatabaseProvider.SqlServer, databaseOptions.Provider);
        Assert.Equal("Server=.;Database=Helper;", databaseOptions.ConnectionString);
    }

    [Fact]
    public void ToDatabaseOptions_BuildsFromProfile_WhenConnectionStringMissing()
    {
        var active = new ActiveDataSourceOptions
        {
            Provider = DatabaseProvider.PostgreSql,
            Postgres =
            {
                Database = "helpersvc",
                Username = "svc",
                Password = "pw"
            }
        };

        var databaseOptions = active.ToDatabaseOptions();

        Assert.Equal(DatabaseProvider.PostgreSql, databaseOptions.Provider);
        Assert.Contains("Database=helpersvc", databaseOptions.ConnectionString);
        Assert.Contains("Username=svc", databaseOptions.ConnectionString);
    }

    [Fact]
    public void AddDataAccessLayer_CanUseActiveDataSourceOptions()
    {
        var services = new ServiceCollection();
        var active = new ActiveDataSourceOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=.;Database=Helper;"
        };

        services.AddDataAccessLayer(active);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<DatabaseOptions>();

        Assert.Equal(DatabaseProvider.SqlServer, options.Provider);
        Assert.Equal("Server=.;Database=Helper;", options.ConnectionString);
    }
}
