namespace Shared.Configuration;

/// <summary>
/// Configures how the DAL normalizes parameter values before binding them to provider commands.
/// </summary>
public sealed class ParameterBindingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether string parameters should be trimmed before sending to the provider.
    /// </summary>
    public bool TrimStrings { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether enum values should be coerced to their underlying numeric type.
    /// </summary>
    public bool ConvertEnumsToUnderlyingType { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether provider-specific type names may include whitespace or SQL control characters.
    /// When false (default) the binder rejects suspicious names to avoid SQL injection through type metadata.
    /// </summary>
    public bool AllowUnsafeProviderTypeNames { get; init; }
}
