using Microsoft.Extensions.DependencyInjection;

namespace DataAccessLayer.Transactions;

internal static class TransactionServiceCollectionExtensions
{
    public static void AddDalTransactions(this IServiceCollection services)
    {
        // Savepoint manager is stateless, transaction manager is scoped to capture unit-of-work boundaries.
        services.AddSingleton<ISavepointManager, SavepointManager>();
        services.AddScoped<ITransactionManager, TransactionManager>();
    }
}
