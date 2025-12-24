using System.Collections.Generic;
using Shared.Configuration;

namespace DataAccessLayer.Configuration;

/// <summary>
/// Centralized manifest for optional DAL subsystems.
/// </summary>
public sealed record DalFeatures
{
    public static DalFeatures Default { get; } = new();

    /// <summary>
    /// Controls IDataAccessTelemetry (activities, metrics).
    /// </summary>
    public bool Telemetry { get; init; } = true;

    /// <summary>
    /// Enables verbose command logging.
    /// </summary>
    public bool DetailedLogging { get; init; }

    /// <summary>
    /// Controls FluentValidation validators.
    /// </summary>
    public bool Validation { get; init; } = true;

    /// <summary>
    /// Controls registration of bulk engines and helpers.
    /// </summary>
    public bool BulkEngines { get; init; } = true;

    /// <summary>
    /// Optional whitelist for bulk engines. Null or empty means all providers.
    /// </summary>
    public HashSet<DatabaseProvider>? EnabledBulkProviders { get; init; }

    /// <summary>
    /// Controls EF-specific helper registrations.
    /// </summary>
    public bool EfHelpers { get; init; } = true;

    /// <summary>
    /// Controls registration of transaction infrastructure.
    /// </summary>
    public bool Transactions { get; init; } = true;

    /// <summary>
    /// Controls resiliency policies (retry strategies).
    /// </summary>
    public bool Resilience { get; init; } = true;
}
