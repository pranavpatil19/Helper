namespace Shared.Entities;

public sealed class TodoItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedUtc { get; set; }
}
