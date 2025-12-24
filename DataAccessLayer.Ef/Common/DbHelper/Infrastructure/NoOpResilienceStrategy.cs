using Polly;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Fallback strategy that disables all retries while keeping the DAL surface consistent.
/// </summary>
public sealed class NoOpResilienceStrategy : IResilienceStrategy
{
    public static NoOpResilienceStrategy Instance { get; } = new();

    private NoOpResilienceStrategy()
    {
        CommandAsyncPolicy = Policy.NoOpAsync();
        CommandSyncPolicy = Policy.NoOp();
        TransactionAsyncPolicy = Policy.NoOpAsync();
        TransactionSyncPolicy = Policy.NoOp();
    }

    public IAsyncPolicy CommandAsyncPolicy { get; }
    public ISyncPolicy CommandSyncPolicy { get; }
    public IAsyncPolicy TransactionAsyncPolicy { get; }
    public ISyncPolicy TransactionSyncPolicy { get; }
}
