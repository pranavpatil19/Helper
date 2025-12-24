using CoreBusiness.Models;

namespace CoreBusiness.Services;

public interface ITodoService
{
    Task<TodoSummary> CreateAsync(TodoCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TodoSummary>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<TodoSummary?> CompleteAsync(Guid id, CancellationToken cancellationToken = default);
}
