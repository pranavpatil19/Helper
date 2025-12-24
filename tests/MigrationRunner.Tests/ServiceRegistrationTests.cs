using System.Threading.Tasks;
using CoreBusiness;
using CoreBusiness.Validation;
using DataAccessLayer.Database.ECM.Interfaces;
using DataAccessLayer.Database.ECM.DbContexts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MigrationRunner;
using MigrationRunner.Configuration;
using Shared.Configuration;
using Xunit;

namespace MigrationRunner.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public async Task AddSourceServices_RegistersEcmDbContextFactory()
    {
        var services = new ServiceCollection();

        services.AddSourceServices(
            EndpointRegistration.FromOptions(new DatabaseEndpointOptions
            {
                Provider = DatabaseProvider.SqlServer,
                ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=RunnerTest;Integrated Security=true;TrustServerCertificate=True"
            }));

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IEcmDbContextFactory>();

        await using var context = await factory.CreateDbContextAsync();
        Assert.IsType<EcmSqlServerDbContext>(context);

        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<EndpointRuntimeOptions>>();
        var runtime = optionsMonitor.Get(EndpointRuntimeOptionNames.Source);
        Assert.Equal("Server=(localdb)\\MSSQLLocalDB;Database=RunnerTest;Integrated Security=true;TrustServerCertificate=True", runtime.Database.ConnectionString);
    }

    [Fact]
    public async Task AddDestinationServices_RegistersEcmDbContextFactory()
    {
        var services = new ServiceCollection();

        services.AddDestinationServices(
            EndpointRegistration.FromOptions(new DatabaseEndpointOptions
            {
                Provider = DatabaseProvider.PostgreSql,
                ConnectionString = "Host=localhost;Database=runner;Username=postgres;Password=postgres"
            }));

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IEcmDbContextFactory>();

        await using var context = await factory.CreateDbContextAsync();
        Assert.IsType<EcmPostgresDbContext>(context);

        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<EndpointRuntimeOptions>>();
        var runtime = optionsMonitor.Get(EndpointRuntimeOptionNames.Destination);
        Assert.Equal("Host=localhost;Database=runner;Username=postgres;Password=postgres", runtime.Database.ConnectionString);
    }

    [Fact]
    public void AddSourceServices_RespectsValidationConfiguration()
    {
        var services = new ServiceCollection();

        services.AddSourceServices(
            EndpointRegistration.FromOptions(
                new DatabaseEndpointOptions
                {
                    Provider = DatabaseProvider.SqlServer,
                    ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=RunnerTest;Integrated Security=true;TrustServerCertificate=True"
                },
                configureValidation: options =>
                {
                    options.Enabled = false;
                    options.DefaultRuleSets = "Strict";
                }));

        var provider = services.BuildServiceProvider();
        var validation = provider.GetRequiredService<IOptions<ValidationOptions>>().Value;
        Assert.False(validation.Enabled);
        Assert.Equal("Strict", validation.DefaultRuleSets);
    }

    [Fact]
    public void AddDestinationServices_RespectsValidationConfiguration()
    {
        var services = new ServiceCollection();

        services.AddDestinationServices(
            EndpointRegistration.FromOptions(
                new DatabaseEndpointOptions
                {
                    Provider = DatabaseProvider.PostgreSql,
                    ConnectionString = "Host=localhost;Database=runner;Username=postgres;Password=postgres"
                },
                configureValidation: options =>
                {
                    options.Enabled = false;
                    options.DefaultRuleSets = "Strict";
                }));

        var provider = services.BuildServiceProvider();
        var validation = provider.GetRequiredService<IOptions<ValidationOptions>>().Value;
        Assert.False(validation.Enabled);
        Assert.Equal("Strict", validation.DefaultRuleSets);
    }

    [Fact]
    public async Task EndpointDbContextFactories_AreIndependent()
    {
        var services = new ServiceCollection();

        services
            .AddSourceDbContextFactory(
                EndpointRegistration.FromOptions(
                    new DatabaseEndpointOptions
                    {
                        Provider = DatabaseProvider.SqlServer,
                        ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=RunnerSource;Integrated Security=true;TrustServerCertificate=True"
                    }))
            .AddDestinationDbContextFactory(
                EndpointRegistration.FromOptions(
                    new DatabaseEndpointOptions
                    {
                        Provider = DatabaseProvider.SqlServer,
                        ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=RunnerDestination;Integrated Security=true;TrustServerCertificate=True"
                    }));

        var provider = services.BuildServiceProvider();
        var sourceFactory = provider.GetRequiredService<ISourceDbContextFactory>();
        var destinationFactory = provider.GetRequiredService<IDestinationDbContextFactory>();

        await using var sourceContext = await sourceFactory.CreateDbContextAsync();
        await using var destinationContext = await destinationFactory.CreateDbContextAsync();

        Assert.Equal("Server=(localdb)\\MSSQLLocalDB;Database=RunnerSource;Integrated Security=true;TrustServerCertificate=True", sourceContext.Database.GetConnectionString());
        Assert.Equal("Server=(localdb)\\MSSQLLocalDB;Database=RunnerDestination;Integrated Security=true;TrustServerCertificate=True", destinationContext.Database.GetConnectionString());

        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<EndpointRuntimeOptions>>();
        Assert.Equal("Server=(localdb)\\MSSQLLocalDB;Database=RunnerSource;Integrated Security=true;TrustServerCertificate=True", optionsMonitor.GetSourceOptions().Database.ConnectionString);
        Assert.Equal("Server=(localdb)\\MSSQLLocalDB;Database=RunnerDestination;Integrated Security=true;TrustServerCertificate=True", optionsMonitor.GetDestinationOptions().Database.ConnectionString);
    }
}
