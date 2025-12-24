namespace CoreBusiness.Models;

/// <summary>
/// Describes the shared fields that to-do validation depends on.
/// </summary>
public interface ITodoRequest
{
    string Title { get; }
    string? Notes { get; }
}
