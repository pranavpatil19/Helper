using Microsoft.Extensions.DependencyInjection;

namespace DataAccessLayer.Telemetry;

internal static class TelemetryServiceCollectionExtensions
{
    public static void AddDalTelemetry(this IServiceCollection services, bool enableTelemetry)
    {
        if (enableTelemetry)
        {
            services.AddSingleton<IDataAccessTelemetry, DataAccessTelemetry>();
        }
        else
        {
            services.AddSingleton<IDataAccessTelemetry, NoOpDataAccessTelemetry>();
        }
    }
}
