using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Configuration;
using DataAccessLayer.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests.Factories;

public sealed class DalHelperFactoryTests
{
    [Fact]
    public async Task Create_WithDatabaseOptions_ResolvesHelperAndTransactionManager()
    {
        await using var scope = DalHelperFactory.Create(CreateOptions());

        Assert.NotNull(scope.DatabaseHelper);
        Assert.NotNull(scope.TransactionManager);
    }

    [Fact]
    public async Task Create_WithActiveDataSourceOptions_UsesProviderProfiles()
    {
        var active = new ActiveDataSourceOptions
        {
            Provider = DatabaseProvider.PostgreSql,
            Postgres =
            {
                Host = "localhost",
                Database = "helpersvc",
                Username = "svc",
                Password = "pw"
            }
        };

        await using var scope = DalHelperFactory.Create(active);
        var options = scope.Services.GetRequiredService<DatabaseOptions>();

        Assert.Equal(DatabaseProvider.PostgreSql, options.Provider);
        Assert.Contains("Database=helpersvc", options.ConnectionString);
    }

    [Fact]
    public async Task Create_WithInlineProvider_AllowsHelperCustomization()
    {
        await using var scope = DalHelperFactory.Create(
            DatabaseProvider.Oracle,
            "Data Source=helper;User Id=svc;Password=pw;",
            configureHelper: helper => helper.IgnoreCase = false);

        var helperOptions = scope.Services.GetRequiredService<DbHelperOptions>();
        Assert.False(helperOptions.IgnoreCase);
    }

    [Fact]
    public async Task Create_WithExpectedProvider_ReturnsHelper()
    {
        await using var scope = DalHelperFactory.Create(
            CreateOptions(),
            DatabaseProvider.SqlServer,
            out var helper,
            out var transactionManager);

        Assert.NotNull(helper);
        Assert.NotNull(transactionManager);
    }

    [Fact]
    public void Create_WithMismatchedProvider_ThrowsProviderNotSupported()
    {
        var options = CreateOptions(DatabaseProvider.PostgreSql);

        Assert.Throws<ProviderNotSupportedException>(() =>
            DalHelperFactory.Create(
                options,
                DatabaseProvider.SqlServer,
                out _,
                out _));
    }

    private static DatabaseOptions CreateOptions(DatabaseProvider provider = DatabaseProvider.SqlServer) =>
        new()
        {
            Provider = provider,
            ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Helper;Integrated Security=true;",
            WrapProviderExceptions = false
        };
}
