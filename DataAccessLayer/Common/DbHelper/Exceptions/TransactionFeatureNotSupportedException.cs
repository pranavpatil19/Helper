using System;

namespace DataAccessLayer.Exceptions;

/// <summary>
/// Thrown when a transaction-related feature (savepoints, scope promotion, etc.) is not supported.
/// </summary>
public sealed class TransactionFeatureNotSupportedException : NotSupportedException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionFeatureNotSupportedException"/> class.
    /// </summary>
    public TransactionFeatureNotSupportedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionFeatureNotSupportedException"/> class.
    /// </summary>
    public TransactionFeatureNotSupportedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
