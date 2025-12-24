namespace CoreBusiness.Models;

public sealed record TodoSummary(Guid Id, string Title, bool IsCompleted, DateTimeOffset CreatedUtc);
