using System;

namespace DataAccessLayer.Exceptions;

/// <summary>
/// Thrown when a requested database provider or feature is not supported by the DAL.
/// </summary>
public sealed class ProviderNotSupportedException : NotSupportedException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderNotSupportedException"/> class.
    /// </summary>
    public ProviderNotSupportedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderNotSupportedException"/> class.
    /// </summary>
    public ProviderNotSupportedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
