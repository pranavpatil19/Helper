using System;

namespace DataAccessLayer.Exceptions;

/// <summary>
/// Represents runtime failures tied to provider-specific behaviors (e.g., driver quirks, missing results).
/// </summary>
public sealed class ProviderFeatureException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderFeatureException"/> class.
    /// </summary>
    public ProviderFeatureException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderFeatureException"/> class.
    /// </summary>
    public ProviderFeatureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
