using Microsoft.Extensions.Options;
using Shared.Configuration;

namespace MigrationRunner;

public static class EndpointRuntimeOptionsExtensions
{
    public static EndpointRuntimeOptions GetSourceOptions(this IOptionsMonitor<EndpointRuntimeOptions> monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return monitor.Get(EndpointRuntimeOptionNames.Source);
    }

    public static EndpointRuntimeOptions GetDestinationOptions(this IOptionsMonitor<EndpointRuntimeOptions> monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return monitor.Get(EndpointRuntimeOptionNames.Destination);
    }
}
