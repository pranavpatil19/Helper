using Microsoft.Extensions.Logging;

namespace MigrationRunner.Infrastructure;

public interface IUserSynchronizationService
{
    Task<int> SynchronizeAsync(CancellationToken cancellationToken = default);
}

internal sealed class UserSynchronizationService(
    ISourceUserDataGateway sourceGateway,
    IDestinationUserDataGateway destinationGateway,
    ILogger<UserSynchronizationService> logger) : IUserSynchronizationService
{
    public async Task<int> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var users = await sourceGateway.GetUsersAsync(cancellationToken).ConfigureAwait(false);
        if (users.Count == 0)
        {
            logger.LogInformation("Source endpoint returned no users to synchronize.");
            return 0;
        }

        logger.LogInformation("Synchronizing {Count} users between source and destination endpoints.", users.Count);
        var affectedRows = await destinationGateway.UpsertAsync(users, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("User synchronization completed. {Count} rows affected at destination.", affectedRows);
        return affectedRows;
    }
}
