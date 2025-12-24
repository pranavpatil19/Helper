namespace Shared.Configuration;

/// <summary>
/// Configures retry/telemetry behavior for database operations.
/// </summary>
public sealed class ResilienceOptions
{
    /// <summary>
    /// Gets or sets whether command retries are enabled.
    /// </summary>
    public bool EnableCommandRetries { get; init; } = true;

    /// <summary>
    /// Gets or sets the maximum number of command retry attempts.
    /// </summary>
    public int CommandRetryCount { get; init; } = 3;

    /// <summary>
    /// Gets or sets the base delay (ms) used for exponential backoff in command retries.
    /// </summary>
    public double CommandBaseDelayMilliseconds { get; init; } = 100;

    /// <summary>
    /// Gets or sets whether transaction retries are enabled.
    /// </summary>
    public bool EnableTransactionRetries { get; init; } = true;

    /// <summary>
    /// Gets or sets the maximum number of transaction retry attempts.
    /// </summary>
    public int TransactionRetryCount { get; init; } = 2;

    /// <summary>
    /// Gets or sets the base delay (ms) used for exponential backoff in transaction retries.
    /// </summary>
    public double TransactionBaseDelayMilliseconds { get; init; } = 250;
}
