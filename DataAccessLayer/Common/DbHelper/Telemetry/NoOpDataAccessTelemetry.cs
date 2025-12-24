using System.Diagnostics;
using DataAccessLayer.Configuration;
using DataAccessLayer.Execution;
using Shared.Configuration;

namespace DataAccessLayer.Telemetry;

internal sealed class NoOpDataAccessTelemetry : IDataAccessTelemetry
{
    public Activity? StartCommandActivity(string operation, DbCommandRequest request, DatabaseOptions defaultOptions) => null;

    public void RecordCommandResult(Activity? activity, DbExecutionResult execution, int? resultCount = null) { }

    public Activity? StartBulkActivity(DatabaseProvider provider, string destinationTable) => null;

    public void RecordBulkResult(Activity? activity, int rowsInserted) { }

    public string GetCommandDisplayName(DbCommandRequest request) => request.TraceName ?? "[redacted]";
}
