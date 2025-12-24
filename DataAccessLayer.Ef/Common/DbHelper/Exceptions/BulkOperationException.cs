using System;

namespace DataAccessLayer.Exceptions;

/// <summary>
/// Represents failures that occur while configuring or executing bulk data operations.
/// </summary>
public sealed class BulkOperationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BulkOperationException"/> class.
    /// </summary>
    public BulkOperationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkOperationException"/> class.
    /// </summary>
    public BulkOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
