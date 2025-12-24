using System;

namespace DataAccessLayer.Exceptions;

/// <summary>
/// Represents failures encountered while executing database commands through the DAL.
/// </summary>
public sealed class DataException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataException"/> class.
    /// </summary>
    /// <param name="message">Descriptive message.</param>
    public DataException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataException"/> class.
    /// </summary>
    /// <param name="message">Descriptive message.</param>
    /// <param name="innerException">Original provider exception.</param>
    public DataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
