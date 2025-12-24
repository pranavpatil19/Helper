namespace Shared.Configuration;

/// <summary>
/// Runtime options exposed to application layers (Core/DAL) for a migration endpoint.
/// </summary>
public sealed class EndpointRuntimeOptions
{
    public DatabaseOptions Database { get; set; } = null!;
}
