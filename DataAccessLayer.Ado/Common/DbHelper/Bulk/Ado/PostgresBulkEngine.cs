using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Telemetry;
using Shared.Configuration;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

public sealed class PostgresBulkEngine : IBulkEngine
{
    private readonly IConnectionScopeManager _scopeManager;
    private readonly IPostgresCopyClientFactory _clientFactory;
    private readonly IDataAccessTelemetry _telemetry;

    public PostgresBulkEngine(
        IConnectionScopeManager scopeManager,
        IPostgresCopyClientFactory clientFactory,
        IDataAccessTelemetry telemetry)
    {
        _scopeManager = scopeManager ?? throw new ArgumentNullException(nameof(scopeManager));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    }

    public DatabaseProvider Provider => DatabaseProvider.PostgreSql;

    public async Task<BulkExecutionResult> ExecuteAsync<T>(
        BulkOperation<T> operation,
        IReadOnlyList<T> rows,
        DatabaseOptions providerOptions,
        CancellationToken cancellationToken)
    {
        EnsureInsertOnly(operation);
        var activity = _telemetry.StartBulkActivity(Provider, operation.Mapping.TableName);

        var writerOptions = new PostgresBulkWriterOptions<T>
        {
            DestinationTable = operation.Mapping.TableName,
            ColumnNames = operation.Mapping.Columns.Select(c => c.ColumnName).ToArray(),
            ValueSelector = operation.Mapping.ValueSelector,
            OverrideOptions = providerOptions
        };

        await using var scope = await _scopeManager.LeaseAsync(providerOptions, cancellationToken).ConfigureAwait(false);
        var writer = new PostgresBulkWriter<T>(scope.Connection, writerOptions, _clientFactory);
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
