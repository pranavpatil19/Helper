using System.Threading.Tasks;
using CoreBusiness.Validation;
using DataAccessLayer.Database.ECM.DbContexts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MigrationRunner;
using MigrationRunner.Configuration;
using MigrationRunner.Infrastructure;
using Shared.Configuration;
using Xunit;

namespace MigrationRunner.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public async Task AddMigrationRunnerServices_RegistersProviderSpecificDbContextFactories()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMigrationRunnerServices(
            EndpointRegistration.FromOptions(new DatabaseEndpointOptions
            {
                Provider = DatabaseProvider.SqlServer,
                SqlServer = new SqlServerConnectionProfileOptions
                {
                    Server = "(localdb)\\MSSQLLocalDB",
                    Database = "RunnerSource",
                    TrustedConnection = true,
                    TrustServerCertificate = true,
                    MultipleActiveResultSets = true
                }
            }),
            EndpointRegistration.FromOptions(new DatabaseEndpointOptions
            {
                Provider = DatabaseProvider.PostgreSql,
                Postgres = new PostgresConnectionProfileOptions
                {
                    Host = "localhost",
                    Port = 5432,
                    Database = "runnerdest",
                    Username = "postgres",
                    Password = "postgres",
                    Pooling = true,
                    SslMode = PostgresSslMode.Prefer
                }
            }));

        var provider = services.BuildServiceProvider();
        var sourceFactory = provider.GetRequiredService<ISourceDbContextFactory>();
        var destinationFactory = provider.GetRequiredService<IDestinationDbContextFactory>();

        await using var sourceContext = await sourceFactory.CreateDbContextAsync();
        await using var destinationContext = await destinationFactory.CreateDbContextAsync();

        Assert.IsType<EcmSqlServerDbContext>(sourceContext);
        Assert.IsType<EcmPostgresDbContext>(destinationContext);

        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<EndpointRuntimeOptions>>();
        Assert.Equal("Server=(localdb)\\MSSQLLocalDB;Database=RunnerSource;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True", optionsMonitor.GetSourceOptions().Database.ConnectionString);
        Assert.Equal("Host=localhost;Port=5432;Database=runnerdest;Username=postgres;Password=postgres;Pooling=True;Ssl Mode=Prefer", optionsMonitor.GetDestinationOptions().Database.ConnectionString);
    }

    [Fact]
    public void AddMigrationRunnerServices_AppliesValidationConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMigrationRunnerServices(
            EndpointRegistration.FromOptions(new DatabaseEndpointOptions
            {
                Provider = DatabaseProvider.SqlServer,
                SqlServer = new SqlServerConnectionProfileOptions
                {
                    Server = "(localdb)\\MSSQLLocalDB",
                    Database = "RunnerTest",
                    TrustedConnection = true,
                    TrustServerCertificate = true
                }
            }),
            EndpointRegistration.FromOptions(
                new DatabaseEndpointOptions
                {
                    Provider = DatabaseProvider.PostgreSql,
                    Postgres = new PostgresConnectionProfileOptions
                    {
                        Host = "localhost",
                        Port = 5432,
                        Database = "runner",
                        Username = "postgres",
                        Password = "postgres"
                    }
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
    public void AddMigrationRunnerServices_RegistersProviderIndependentServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMigrationRunnerServices(
            EndpointRegistration.FromOptions(new DatabaseEndpointOptions
            {
                Provider = DatabaseProvider.SqlServer,
                SqlServer = new SqlServerConnectionProfileOptions
                {
                    Server = "(localdb)\\MSSQLLocalDB",
                    Database = "RunnerSource",
                    TrustedConnection = true,
                    TrustServerCertificate = true
                }
            }),
            EndpointRegistration.FromOptions(new DatabaseEndpointOptions
            {
                Provider = DatabaseProvider.SqlServer,
                SqlServer = new SqlServerConnectionProfileOptions
                {
                    Server = "(localdb)\\MSSQLLocalDB",
                    Database = "RunnerDestination",
                    TrustedConnection = true,
                    TrustServerCertificate = true
                }
            }));

        var provider = services.BuildServiceProvider();

        var syncService = provider.GetRequiredService<IUserSynchronizationService>();
        Assert.NotNull(syncService);

        var hostedServices = provider.GetServices<IHostedService>();
        Assert.Contains(hostedServices, service => service.GetType().Name == "MigrationHostedService");
    }
}
