using Polly;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Provides retry/telemetry policies for database commands and transactions.
/// </summary>
public interface IResilienceStrategy
{
    IAsyncPolicy CommandAsyncPolicy { get; }
    ISyncPolicy CommandSyncPolicy { get; }
    IAsyncPolicy TransactionAsyncPolicy { get; }
    ISyncPolicy TransactionSyncPolicy { get; }
}
