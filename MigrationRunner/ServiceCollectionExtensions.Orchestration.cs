using DataAccessLayer.Database.ECM.DbContexts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MigrationRunner.Infrastructure;

namespace MigrationRunner;

public static partial class ServiceCollectionExtensions
{
    private static void RegisterMigrationOrchestratorServices(this IServiceCollection services)
    {
        // Gateways and synchronization service are scoped so each migration run gets fresh DbContext instances and logging scopes.
        services
            .AddScoped<ISourceUserDataGateway>(sp =>
                new EndpointUserDataGateway(
                    sp.GetRequiredService<ISourceDbContextFactory>(),
                    sp.GetRequiredService<ILogger<EndpointUserDataGateway>>()))
            .AddScoped<IDestinationUserDataGateway>(sp =>
                new EndpointUserDataGateway(
                    sp.GetRequiredService<IDestinationDbContextFactory>(),
                    sp.GetRequiredService<ILogger<EndpointUserDataGateway>>()))
            .AddScoped<IUserSynchronizationService, UserSynchronizationService>()
            .AddHostedService<MigrationHostedService>();
    }
}
