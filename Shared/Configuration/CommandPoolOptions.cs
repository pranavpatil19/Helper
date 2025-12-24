namespace Shared.Configuration;

/// <summary>
/// Configures command/parameter pooling behavior for the DAL infrastructure.
/// </summary>
public sealed class CommandPoolOptions
{
    /// <summary>
    /// Gets or sets whether DbCommand pooling is enabled. Defaults to true.
    /// </summary>
    public bool EnableCommandPooling { get; init; } = true;

    /// <summary>
    /// Gets or sets whether DbParameter objects are pooled. Defaults to false.
    /// </summary>
    public bool EnableParameterPooling { get; init; }

    /// <summary>
    /// Gets or sets the maximum retained command instances per provider pool.
    /// </summary>
    public int MaximumRetainedCommands { get; init; } = 128;

    /// <summary>
    /// Gets or sets the maximum retained parameter instances per provider pool.
    /// </summary>
    public int MaximumRetainedParameters { get; init; } = 512;
}
