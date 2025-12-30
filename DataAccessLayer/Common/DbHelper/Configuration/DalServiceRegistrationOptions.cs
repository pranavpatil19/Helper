using System;

namespace DataAccessLayer.Configuration;

/// <summary>
/// Configures which optional DAL services are wired up during DI registration.
/// </summary>
public sealed class DalServiceRegistrationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether telemetry and diagnostics pipelines are enabled.
    /// Defaults to <c>false</c> to avoid accidental noise in production logs.
    /// </summary>
    public bool EnableTelemetry { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether retries and circuit breakers are enabled.
    /// </summary>
    public bool EnableResilience { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether DatabaseHelper should emit informational command lifecycle logs.
    /// </summary>
    public bool EnableDetailedLogging { get; set; }

    internal static DalServiceRegistrationOptions Create(Action<DalServiceRegistrationOptions>? configure)
    {
        var options = new DalServiceRegistrationOptions();
        configure?.Invoke(options);
        return options;
    }
}
