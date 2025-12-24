using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using DataAccessLayer.Configuration;
using DataAccessLayer.Execution;
using DataAccessLayer.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Ado.Tests;

public class DependencyInjectionSmokeTests
{
    [Fact]
    public void AddDataAccessLayer_RegistersCoreAdoServices()
    {
        var services = CreateServices();
        var options = CreateOptions();

        services.AddDataAccessLayer(options);
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IDatabaseHelper>());
        Assert.NotNull(provider.GetService<IBulkWriteHelper>());
        Assert.NotNull(provider.GetService<ITransactionManager>());
    }

    [Fact]
    public void FeaturesDisableBulkEngines_WhenSetToFalse()
    {
        var services = CreateServices();
        var options = CreateOptions();

        using var featureOverride = DalFeatureDefaults.Override(_ => DalFeatures.Default with { BulkEngines = false });

        services.AddDataAccessLayer(options);

        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IBulkWriteHelper>());
        Assert.NotNull(provider.GetService<IDatabaseHelper>()); // sanity
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static DatabaseOptions CreateOptions() => new()
    {
        Provider = DatabaseProvider.SqlServer,
        ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Helper;Integrated Security=true;"
    };
}
