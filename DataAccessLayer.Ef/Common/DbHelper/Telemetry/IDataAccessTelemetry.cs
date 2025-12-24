using System.Diagnostics;
using DataAccessLayer.Configuration;
using DataAccessLayer.Execution;
using Shared.Configuration;

namespace DataAccessLayer.Telemetry;

/// <summary>
/// Provides a shared gateway for DAL telemetry (activities, metrics, sanitized logging helpers).
/// </summary>
public interface IDataAccessTelemetry
{
    /// <summary>
    /// Starts an OpenTelemetry-compatible activity for a database command.
    /// </summary>
    Activity? StartCommandActivity(string operation, DbCommandRequest request, DatabaseOptions defaultOptions);

    /// <summary>
    /// Records metadata about the executed command (rows affected, outputs, result counts).
    /// </summary>
    void RecordCommandResult(Activity? activity, DbExecutionResult execution, int? resultCount = null);

    /// <summary>
    /// Starts an activity describing a bulk write operation.
    /// </summary>
    Activity? StartBulkActivity(DatabaseProvider provider, string destinationTable);

    /// <summary>
    /// Records metrics for a bulk operation.
    /// </summary>
    void RecordBulkResult(Activity? activity, int rowsInserted);

    /// <summary>
    /// Provides a sanitized command name for logging scopes (command text is redacted unless explicitly enabled).
    /// </summary>
    string GetCommandDisplayName(DbCommandRequest request);
}
