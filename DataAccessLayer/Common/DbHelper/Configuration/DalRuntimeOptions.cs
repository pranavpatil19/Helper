namespace DataAccessLayer.Configuration;

/// <summary>
/// Runtime-only toggles that keep DatabaseHelper behavior easy to reason about.
/// </summary>
public sealed class DalRuntimeOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether DatabaseHelper emits informational command logs.
    /// </summary>
    public bool EnableDetailedLogging { get; init; }
}
