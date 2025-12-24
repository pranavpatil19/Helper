using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Configuration;
using DataAccessLayer.Database.ECM.Interfaces;
using DataAccessLayer.Execution;
using DataAccessLayer.EF;
using DataAccessLayer.Telemetry;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Ef.Tests;

public class EfFeatureSupportTests
{
    [Fact]
    public async Task AddEcmEntityFrameworkSupport_RegistersFeatureInfrastructure()
    {
        var services = CreateServices();
        var options = CreateOptions();

        services.AddEcmEntityFrameworkSupport(options);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IEcmDbContextFactory>();

        Assert.NotNull(provider.GetService<IDataAccessTelemetry>());
        Assert.NotNull(provider.GetService<IValidator<DbCommandRequest>>());
        Assert.NotNull(provider.GetService<IResilienceStrategy>());
        Assert.NotNull(factory);

        await using var context = await factory!.CreateDbContextAsync();
        Assert.NotNull(context);
    }

    [Fact]
    public async Task ResilienceStrategy_ExecutesPolicies()
    {
        var services = CreateServices();
        var options = CreateOptions();

        services.AddEcmEntityFrameworkSupport(options);

        using var provider = services.BuildServiceProvider();
        var resilience = provider.GetRequiredService<IResilienceStrategy>();

        var executed = false;
        await resilience.TransactionAsyncPolicy.ExecuteAsync(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        Assert.True(executed);
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
