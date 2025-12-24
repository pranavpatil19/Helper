using DataAccessLayer.EF;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MigrationRunner.Infrastructure;

namespace MigrationRunner;

internal sealed class MigrationHostedService(
    IServiceProvider serviceProvider,
    ILogger<MigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
        var userSynchronizationService = scope.ServiceProvider.GetRequiredService<IUserSynchronizationService>();

        logger.LogInformation("Starting database migration runner.");
        await migrationService.ApplyMigrationsAsync(cancellationToken).ConfigureAwait(false);
        var synchronized = await userSynchronizationService.SynchronizeAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("User synchronization completed with {Count} changes.", synchronized);
        logger.LogInformation("Migration runner finished successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
