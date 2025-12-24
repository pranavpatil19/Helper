using DataAccessLayer.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccessLayer.Telemetry;

internal static class TelemetryServiceCollectionExtensions
{
    public static void AddDalTelemetry(this IServiceCollection services, DalFeatures features)
    {
        if (features.Telemetry)
        {
            services.AddSingleton<IDataAccessTelemetry, DataAccessTelemetry>();
        }
        else
        {
            services.AddSingleton<IDataAccessTelemetry, NoOpDataAccessTelemetry>();
        }
    }
}
