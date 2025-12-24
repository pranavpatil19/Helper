using System;

namespace DataAccessLayer.Exceptions;

/// <summary>
/// Represents configuration or wiring errors detected while bootstrapping the DAL.
/// </summary>
public sealed class DalConfigurationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DalConfigurationException"/> class.
    /// </summary>
    public DalConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DalConfigurationException"/> class.
    /// </summary>
    public DalConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
