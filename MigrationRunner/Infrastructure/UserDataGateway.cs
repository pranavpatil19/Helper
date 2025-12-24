using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Entities;
using DataAccessLayer.Database.ECM.Interfaces;

namespace MigrationRunner.Infrastructure;

internal interface IUserDataGateway
{
    Task<IReadOnlyList<UserProfile>> GetUsersAsync(CancellationToken cancellationToken);
    Task<int> UpsertAsync(IEnumerable<UserProfile> users, CancellationToken cancellationToken);
}

internal interface ISourceUserDataGateway : IUserDataGateway;

internal interface IDestinationUserDataGateway : IUserDataGateway;

internal sealed class EndpointUserDataGateway(
    IEcmDbContextFactory dbContextFactory,
    ILogger<EndpointUserDataGateway> logger) : ISourceUserDataGateway, IDestinationUserDataGateway
{
    public async Task<IReadOnlyList<UserProfile>> GetUsersAsync(CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.UserProfiles
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> UpsertAsync(IEnumerable<UserProfile> users, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(users);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var set = context.UserProfiles;
        var destinationLookup = await set
            .AsTracking()
            .ToDictionaryAsync(u => u.Id, cancellationToken)
            .ConfigureAwait(false);

        foreach (var incoming in users)
        {
            if (destinationLookup.TryGetValue(incoming.Id, out var existing))
            {
                if (NeedsUpdate(existing, incoming))
                {
                    existing.UserName = incoming.UserName;
                    existing.Email = incoming.Email;
                    existing.IsActive = incoming.IsActive;
                    existing.LastUpdatedUtc = incoming.LastUpdatedUtc ?? DateTimeOffset.UtcNow;
                }

                continue;
            }

            await set.AddAsync(Clone(incoming), cancellationToken).ConfigureAwait(false);
        }

        var changed = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Persisted {Count} user changes for provider {Provider}.", changed, context.Database.ProviderName);
        return changed;
    }

    private static bool NeedsUpdate(UserProfile current, UserProfile incoming) =>
        !string.Equals(current.UserName, incoming.UserName, StringComparison.Ordinal) ||
        !string.Equals(current.Email, incoming.Email, StringComparison.OrdinalIgnoreCase) ||
        current.IsActive != incoming.IsActive;

    private static UserProfile Clone(UserProfile source) =>
        new()
        {
            Id = source.Id,
            UserName = source.UserName,
            Email = source.Email,
            IsActive = source.IsActive,
            CreatedUtc = source.CreatedUtc,
            LastUpdatedUtc = source.LastUpdatedUtc
        };
}
