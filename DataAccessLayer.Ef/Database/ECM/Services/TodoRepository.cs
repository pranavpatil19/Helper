using DataAccessLayer.Database.ECM.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.Entities;

namespace DataAccessLayer.Database.ECM.Services;

public sealed class TodoRepository(IEcmDbContextFactory contextFactory) : ITodoRepository
{
    public async Task<IReadOnlyList<TodoItem>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TodoItems
            .Where(item => !item.IsCompleted)
            .OrderBy(item => item.CreatedUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TodoItems
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TodoItem> AddAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.TodoItems.AddAsync(item, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return item;
    }

    public async Task<TodoItem?> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.TodoItems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        entity.IsCompleted = true;
        entity.CompletedUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }
}
