using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using DataAccessLayer.Configuration;
using DataAccessLayer.Execution;
using Shared.Configuration;

namespace DataAccessLayer.Telemetry;

/// <summary>
/// Default telemetry implementation that emits OpenTelemetry activities and lightweight metrics.
/// </summary>
public sealed class DataAccessTelemetry : IDataAccessTelemetry, IDisposable
{
    private readonly DbHelperOptions _options;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    private readonly Counter<long> _bulkRowCounter;

    public DataAccessTelemetry(DbHelperOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _activitySource = new ActivitySource(_options.Telemetry.ActivitySourceName);
        _meter = new Meter(_options.Telemetry.ActivitySourceName, "1.0.0");
        _bulkRowCounter = _meter.CreateCounter<long>("dal.bulk.rows", unit: "rows", description: "Number of rows inserted via bulk operations.");
    }

    public Activity? StartCommandActivity(string operation, DbCommandRequest request, DatabaseOptions defaultOptions)
    {
        var activity = _activitySource.StartActivity(operation, ActivityKind.Client);
        if (activity is null)
        {
            return null;
        }

        var effectiveOptions = request.OverrideOptions ?? defaultOptions;
        activity.SetTag("db.system", effectiveOptions.Provider.ToString());
        activity.SetTag("db.operation", operation);
        activity.SetTag("db.command_type", request.CommandType.ToString());
        activity.SetTag("db.statement.length", request.CommandText?.Length ?? 0);
        if (!string.IsNullOrWhiteSpace(request.TraceName))
        {
            activity.SetTag("db.statement.name", request.TraceName);
        }

        var timeout = request.CommandTimeoutSeconds
            ?? request.OverrideOptions?.CommandTimeoutSeconds
            ?? defaultOptions.CommandTimeoutSeconds;
        if (timeout is { } timeoutSeconds)
        {
            activity.SetTag("db.timeout_seconds", timeoutSeconds);
        }

        return activity;
    }

    public void RecordCommandResult(Activity? activity, DbExecutionResult execution, int? resultCount = null)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("db.rows_affected", execution.RowsAffected);
        if (execution.Scalar is not null)
        {
            activity.SetTag("db.scalar.type", execution.Scalar.GetType().Name);
        }

        if (execution.HasOutputParameters)
        {
            activity.SetTag("db.output.count", execution.OutputParameters.Count);
        }

        if (resultCount is { } count)
        {
            activity.SetTag("db.result.count", count);
        }

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    public Activity? StartBulkActivity(DatabaseProvider provider, string destinationTable)
    {
        var activity = _activitySource.StartActivity("BulkExecute", ActivityKind.Client);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("db.system", provider.ToString());
        activity.SetTag("dal.bulk.table", destinationTable);
        return activity;
    }

    public void RecordBulkResult(Activity? activity, int rowsInserted)
    {
        _bulkRowCounter.Add(rowsInserted);
        if (activity is null)
        {
            return;
        }

        activity.SetTag("dal.bulk.rows", rowsInserted);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    public string GetCommandDisplayName(DbCommandRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.TraceName))
        {
            return request.TraceName!;
        }

        return _options.Telemetry.IncludeCommandTextInLogs
            ? request.CommandText
            : "[redacted]";
    }

    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
    }
}
