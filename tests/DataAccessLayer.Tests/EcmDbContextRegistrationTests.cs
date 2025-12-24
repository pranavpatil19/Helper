using System.Threading.Tasks;
using DataAccessLayer.Configuration;
using DataAccessLayer.Database.ECM.DbContexts;
using DataAccessLayer.Database.ECM.Interfaces;
using DataAccessLayer.Database.ECM.Services;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class EcmDbContextRegistrationTests
{
    [Theory]
    [InlineData(DatabaseProvider.SqlServer, typeof(EcmSqlServerDbContext), "Server=(localdb)\\MSSQLLocalDB;Database=Helper;Integrated Security=true;TrustServerCertificate=True")]
    [InlineData(DatabaseProvider.PostgreSql, typeof(EcmPostgresDbContext), "Host=localhost;Database=helper;Username=postgres;Password=postgres")]
    [InlineData(DatabaseProvider.Oracle, typeof(EcmOracleDbContext), "User Id=system;Password=oracle;Data Source=localhost:1521/xe")]
    public async Task AddEcmEntityFrameworkSupport_RegistersProviderSpecificEcmContext(DatabaseProvider provider, System.Type expectedType, string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var options = new DatabaseOptions
        {
            Provider = provider,
            ConnectionString = connectionString,
            WrapProviderExceptions = false
        };

        services.AddDataAccessLayer(options);
        services.AddEcmEntityFrameworkSupport(options);

        await using var providerInstance = services.BuildServiceProvider();
        var factory = providerInstance.GetRequiredService<IEcmDbContextFactory>();
        await using var context = await factory.CreateDbContextAsync();

        Assert.IsType(expectedType, context);
    }

    [Fact]
    public void AddDataAccessLayer_WithEfHelpersDisabled_DoesNotRegisterContextFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var options = new DatabaseOptions
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Helper;Integrated Security=true;TrustServerCertificate=True",
            WrapProviderExceptions = false
        };

        using var featureOverride = DalFeatureDefaults.Override(_ => DalFeatures.Default with { EfHelpers = false });

        services.AddDataAccessLayer(options);
        services.AddEcmEntityFrameworkSupport(options);

        using var providerInstance = services.BuildServiceProvider();

        Assert.Null(providerInstance.GetService<IEcmDbContextFactory>());
        Assert.Null(providerInstance.GetService<ITodoRepository>());
    }
}
