using CoreBusiness.Models;
using CoreBusiness.Validation;
using DataAccessLayer.Database.ECM.Interfaces;
using FluentValidation;
using Shared.Entities;

namespace CoreBusiness.Services;

public sealed class TodoService(
    ITodoRepository repository,
    IValidationService validationService) : ITodoService
{
    public async Task<TodoSummary> CreateAsync(TodoCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        validationService.ValidateAndThrow(request);

        var entity = new TodoItem
        {
            Title = request.Title.Trim(),
            Notes = request.Notes?.Trim()
        };

        var created = await repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return ToSummary(created);
    }

    public async Task<IReadOnlyList<TodoSummary>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await repository.GetPendingAsync(cancellationToken).ConfigureAwait(false);
        return pending.Select(ToSummary).ToList();
    }

    public async Task<TodoSummary?> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var updated = await repository.CompleteAsync(id, cancellationToken).ConfigureAwait(false);
        return updated is null ? null : ToSummary(updated);
    }

    private static TodoSummary ToSummary(TodoItem item) =>
        new(item.Id, item.Title, item.IsCompleted, item.CreatedUtc);
}
