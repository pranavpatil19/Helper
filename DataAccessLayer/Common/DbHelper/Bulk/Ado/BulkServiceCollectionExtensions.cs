using System.Collections.Generic;

using DataAccessLayer.Configuration;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

internal static class BulkServiceCollectionExtensions
{
    public static void AddDalBulkEngines(this IServiceCollection services, DalFeatures features)
    {
        if (!features.BulkEngines)
        {
            return;
        }

        services.AddSingleton<ISqlBulkCopyClientFactory, SqlBulkCopyClientFactory>();
        services.AddSingleton<IPostgresCopyClientFactory, PostgresCopyClientFactory>();

        if (IsBulkProviderEnabled(features, DatabaseProvider.SqlServer))
        {
            services.AddSingleton<IBulkEngine>(sp =>
                new SqlServerBulkEngine(
                    sp.GetRequiredService<IConnectionScopeManager>(),
                    sp.GetRequiredService<ISqlBulkCopyClientFactory>(),
                    sp.GetRequiredService<IDataAccessTelemetry>()));
        }

        if (IsBulkProviderEnabled(features, DatabaseProvider.PostgreSql))
        {
            services.AddSingleton<IBulkEngine>(sp =>
                new PostgresBulkEngine(
                    sp.GetRequiredService<IConnectionScopeManager>(),
                    sp.GetRequiredService<IPostgresCopyClientFactory>(),
                    sp.GetRequiredService<IDataAccessTelemetry>()));
        }

        if (IsBulkProviderEnabled(features, DatabaseProvider.Oracle))
        {
            services.AddSingleton<IBulkEngine>(sp =>
                new OracleBulkEngine(
                    sp.GetRequiredService<IConnectionScopeManager>(),
                    sp.GetRequiredService<IDataAccessTelemetry>()));
        }

        services.AddSingleton<IBulkWriteHelper>(sp =>
            new BulkWriteHelper(
                sp.GetRequiredService<DatabaseOptions>(),
                sp.GetServices<IBulkEngine>()));
    }

    private static bool IsBulkProviderEnabled(DalFeatures features, DatabaseProvider provider)
    {
        if (features.EnabledBulkProviders is null || features.EnabledBulkProviders.Count == 0)
        {
            return true;
        }

        return features.EnabledBulkProviders.Contains(provider);
    }
}
