using System;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Shared.Configuration;
using DataException = DataAccessLayer.Exceptions.DataException;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Builds Polly-based retry strategies for commands and transactions.
/// </summary>
public sealed class ResilienceStrategy : IResilienceStrategy
{
    private readonly ILogger<ResilienceStrategy> _logger;

    public ResilienceStrategy(ResilienceOptions options, ILogger<ResilienceStrategy> logger)
    {
        options ??= new ResilienceOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        CommandAsyncPolicy = CreateAsyncPolicy(options.EnableCommandRetries, options.CommandRetryCount, options.CommandBaseDelayMilliseconds, "command");
        CommandSyncPolicy = CreateSyncPolicy(options.EnableCommandRetries, options.CommandRetryCount, options.CommandBaseDelayMilliseconds, "command");
        TransactionAsyncPolicy = CreateAsyncPolicy(options.EnableTransactionRetries, options.TransactionRetryCount, options.TransactionBaseDelayMilliseconds, "transaction");
        TransactionSyncPolicy = CreateSyncPolicy(options.EnableTransactionRetries, options.TransactionRetryCount, options.TransactionBaseDelayMilliseconds, "transaction");
    }

    public IAsyncPolicy CommandAsyncPolicy { get; }
    public ISyncPolicy CommandSyncPolicy { get; }
    public IAsyncPolicy TransactionAsyncPolicy { get; }
    public ISyncPolicy TransactionSyncPolicy { get; }

    private AsyncPolicy CreateAsyncPolicy(bool enabled, int retries, double baseDelayMs, string category)
    {
        if (!enabled || retries <= 0)
        {
            return Policy.NoOpAsync();
        }

        return Policy
            .Handle<Exception>(ShouldRetry)
            .WaitAndRetryAsync(
                retries,
                attempt => TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1)),
                (exception, timespan, attempt, context) =>
                    _logger.LogWarning(exception, "Retrying {Category} operation after {Delay} (attempt {Attempt}).", category, timespan, attempt));
    }

    private Policy CreateSyncPolicy(bool enabled, int retries, double baseDelayMs, string category)
    {
        if (!enabled || retries <= 0)
        {
            return Policy.NoOp();
        }

        return Policy
            .Handle<Exception>(ShouldRetry)
            .WaitAndRetry(
                retries,
                attempt => TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1)),
                (exception, timespan, attempt, context) =>
                    _logger.LogWarning(exception, "Retrying {Category} operation after {Delay} (attempt {Attempt}).", category, timespan, attempt));
    }

    private static bool ShouldRetry(Exception exception)
    {
        if (exception is DataException dataException && dataException.InnerException is Exception inner)
        {
            return ShouldRetry(inner);
        }

        return exception switch
        {
            DbException => true,
            TimeoutException => true,
            TaskCanceledException => true,
            _ => false
        };
    }
}
