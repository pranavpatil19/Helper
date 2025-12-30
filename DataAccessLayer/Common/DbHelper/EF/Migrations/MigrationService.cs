using DataAccessLayer.Database.ECM.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccessLayer.EF;

public sealed class MigrationService(IEcmDbContextFactory contextFactory, ILogger<MigrationService> logger) : IMigrationService
{
    public async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Applying EF Core migrations...");
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Database is up to date.");
    }
}
