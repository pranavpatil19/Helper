namespace Shared.Configuration;

/// <summary>
/// Controls diagnostic logging behavior for the DAL.
/// </summary>
public sealed class DiagnosticsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the DAL should log the effective
    /// connection/command timeouts applied to providers and commands.
    /// </summary>
    public bool LogEffectiveTimeouts { get; init; }
}
