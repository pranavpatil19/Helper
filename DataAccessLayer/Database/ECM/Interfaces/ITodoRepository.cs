using Shared.Entities;

namespace DataAccessLayer.Database.ECM.Interfaces;

public interface ITodoRepository
{
    Task<IReadOnlyList<TodoItem>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TodoItem> AddAsync(TodoItem item, CancellationToken cancellationToken = default);
    Task<TodoItem?> CompleteAsync(Guid id, CancellationToken cancellationToken = default);
}
