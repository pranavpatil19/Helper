using System;

namespace DataAccessLayer.Exceptions;

/// <summary>
/// Represents failures while materializing rows into CLR types.
/// </summary>
public sealed class RowMappingException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RowMappingException"/> class.
    /// </summary>
    public RowMappingException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RowMappingException"/> class.
    /// </summary>
    public RowMappingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
