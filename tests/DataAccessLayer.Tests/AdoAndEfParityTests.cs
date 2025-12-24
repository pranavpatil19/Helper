extern alias AdoDal;
extern alias EfDal;

using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests;

public class AdoAndEfParityTests
{
    [Fact]
    public void AdoAssembly_AddDataAccessLayer_RegistersDatabaseHelper()
    {
        var services = CreateServices();
        var options = CreateOptions();

        _ = AdoDal::DataAccessLayer.DependencyInjection.AddDataAccessLayer(services, options);
        using var provider = services.BuildServiceProvider();

        var helper = provider.GetService<AdoDal::DataAccessLayer.Execution.IDatabaseHelper>();
        Assert.NotNull(helper);
    }

    [Fact]
    public void EfAssembly_AddEcmEntityFrameworkSupport_RegistersRepository()
    {
        var services = CreateServices();
        var options = CreateOptions();

        _ = AdoDal::DataAccessLayer.DependencyInjection.AddDataAccessLayer(services, options);
        _ = EfDal::DataAccessLayer.EcmEntityFrameworkServiceCollectionExtensions
            .AddEcmEntityFrameworkSupport(services, options);

        using var provider = services.BuildServiceProvider();
        var repository = provider.GetService<EfDal::DataAccessLayer.Database.ECM.Interfaces.ITodoRepository>();
        Assert.NotNull(repository);
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
