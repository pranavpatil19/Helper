namespace Shared.Entities;

/// <summary>
/// Simple user projection shared by both source and destination databases during migration.
/// </summary>
public sealed class UserProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUpdatedUtc { get; set; }
}
