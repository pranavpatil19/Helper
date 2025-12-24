using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Telemetry;
using Shared.Configuration;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

public sealed class OracleBulkEngine : IBulkEngine
{
    private readonly IConnectionScopeManager _scopeManager;
    private readonly IDataAccessTelemetry _telemetry;

    public OracleBulkEngine(IConnectionScopeManager scopeManager, IDataAccessTelemetry telemetry)
    {
        _scopeManager = scopeManager ?? throw new ArgumentNullException(nameof(scopeManager));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    }

    public DatabaseProvider Provider => DatabaseProvider.Oracle;

    public async Task<BulkExecutionResult> ExecuteAsync<T>(
        BulkOperation<T> operation,
        IReadOnlyList<T> rows,
        DatabaseOptions providerOptions,
        CancellationToken cancellationToken)
    {
        EnsureInsertOnly(operation);
        var activity = _telemetry.StartBulkActivity(Provider, operation.Mapping.TableName);

        var parameterNames = operation.Mapping.Columns
            .Select((_, index) => $":p{index}")
            .ToArray();

        var tableName = operation.Options.DestinationTableOverride ?? operation.Mapping.TableName;
        var columnList = string.Join(", ", operation.Mapping.Columns.Select(c => c.ColumnName));
        var valuesList = string.Join(", ", parameterNames);
        var commandText = $"INSERT INTO {tableName} ({columnList}) VALUES ({valuesList})";

        var writerOptions = new OracleBulkWriterOptions<T>
        {
            CommandText = commandText,
            CommandType = CommandType.Text,
            ParameterNames = parameterNames,
            ParameterDbTypes = operation.Mapping.Columns.Select(c => c.DbType).ToArray(),
            BatchSize = operation.Options.BatchSize ?? 256,
            ValueSelector = operation.Mapping.ValueSelector,
            OverrideOptions = providerOptions
        };

        await using var scope = await _scopeManager.LeaseAsync(providerOptions, cancellationToken).ConfigureAwait(false);
        var writer = new OracleBulkWriter<T>(scope.Connection, writerOptions);
        await writer.WriteAsync(rows, cancellationToken).ConfigureAwait(false);
        _telemetry.RecordBulkResult(activity, rows.Count);
        activity?.Dispose();
        return new BulkExecutionResult(rows.Count, 0, 0);
    }

    private static void EnsureInsertOnly<T>(BulkOperation<T> operation)
    {
        if (operation.Mode != BulkOperationMode.Insert)
        {
            throw new BulkOperationException($"Bulk mode '{operation.Mode}' is not yet supported.");
        }
    }
}
