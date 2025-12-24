namespace CoreBusiness.Models;

/// <summary>
/// Represents the user intent to create a new task entry.
/// </summary>
public sealed record TodoCreateRequest(string Title, string? Notes) : ITodoRequest;
