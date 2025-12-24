using DataAccessLayer.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccessLayer.Transactions;

internal static class TransactionServiceCollectionExtensions
{
    public static void AddDalTransactions(this IServiceCollection services, DalFeatures features)
    {
        if (!features.Transactions)
        {
            return;
        }

        services.AddSingleton<ISavepointManager, SavepointManager>();
        services.AddScoped<ITransactionManager, TransactionManager>();
    }
}
