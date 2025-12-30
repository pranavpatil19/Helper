using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

internal static class BulkServiceCollectionExtensions
{
    public static void AddDalBulkEngines(this IServiceCollection services)
    {
        // Factories are singletons because they only coordinate provider APIs and shared pools.
        services.AddSingleton<ISqlBulkCopyClientFactory, SqlBulkCopyClientFactory>();
        services.AddSingleton<IPostgresCopyClientFactory, PostgresCopyClientFactory>();

        services.AddSingleton<IBulkEngine>(sp =>
            new SqlServerBulkEngine(
                sp.GetRequiredService<IConnectionScopeManager>(),
                sp.GetRequiredService<ISqlBulkCopyClientFactory>(),
                sp.GetRequiredService<IDataAccessTelemetry>()));
        services.AddSingleton<IBulkEngine>(sp =>
            new PostgresBulkEngine(
                sp.GetRequiredService<IConnectionScopeManager>(),
                sp.GetRequiredService<IPostgresCopyClientFactory>(),
                sp.GetRequiredService<IDataAccessTelemetry>()));
        services.AddSingleton<IBulkEngine>(sp =>
            new OracleBulkEngine(
                sp.GetRequiredService<IConnectionScopeManager>(),
                sp.GetRequiredService<IDataAccessTelemetry>()));

        // Singleton helper caches the provider->engine lookup per DatabaseOptions instance.
        services.AddSingleton<IBulkWriteHelper>(sp =>
            new BulkWriteHelper(
                sp.GetRequiredService<DatabaseOptions>(),
                sp.GetServices<IBulkEngine>()));
    }
}
