namespace DataAccessLayer.Configuration;

/// <summary>
/// Controls DAL telemetry and logging behavior.
/// </summary>
public sealed class TelemetryOptions
{
    /// <summary>
    /// Gets or sets the activity source name used for OpenTelemetry spans.
    /// </summary>
    public string ActivitySourceName { get; init; } = "Helper.DataAccessLayer.Database";

    /// <summary>
    /// Gets or sets a value indicating whether raw SQL statements can appear in DAL logs.
    /// When false the helper logs <c>[redacted]</c> unless a <see cref="Execution.DbCommandRequest.TraceName"/> is supplied.
    /// </summary>
    public bool IncludeCommandTextInLogs { get; init; }
}
