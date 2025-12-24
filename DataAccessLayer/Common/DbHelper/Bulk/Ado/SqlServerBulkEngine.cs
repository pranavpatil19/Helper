using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using Microsoft.Data.SqlClient;
using DataAccessLayer.Telemetry;
using Shared.Configuration;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

public sealed class SqlServerBulkEngine : IBulkEngine
{
    private readonly IConnectionScopeManager _scopeManager;
    private readonly ISqlBulkCopyClientFactory _clientFactory;
    private readonly IDataAccessTelemetry _telemetry;

    public SqlServerBulkEngine(
        IConnectionScopeManager scopeManager,
        ISqlBulkCopyClientFactory clientFactory,
        IDataAccessTelemetry telemetry)
    {
        _scopeManager = scopeManager ?? throw new ArgumentNullException(nameof(scopeManager));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    }

    public DatabaseProvider Provider => DatabaseProvider.SqlServer;

    public async Task<BulkExecutionResult> ExecuteAsync<T>(
        BulkOperation<T> operation,
        IReadOnlyList<T> rows,
        DatabaseOptions providerOptions,
        CancellationToken cancellationToken)
    {
        EnsureInsertOnly(operation);
        var activity = _telemetry.StartBulkActivity(Provider, operation.Mapping.TableName);

        var writerOptions = new SqlServerBulkWriterOptions<T>
        {
            DestinationTable = operation.Options.DestinationTableOverride ?? operation.Mapping.TableName,
            ColumnNames = operation.Mapping.Columns.Select(c => c.ColumnName).ToArray(),
            Columns = operation.Mapping.Columns,
            ValueSelector = operation.Mapping.ValueSelector,
            OverrideOptions = providerOptions,
            BulkCopyOptions = BuildSqlBulkCopyOptions(operation.Options),
            BatchSize = operation.Options.BatchSize,
            BulkCopyTimeoutSeconds = operation.Options.CommandTimeoutSeconds
        };

        await using var scope = await _scopeManager.LeaseAsync(providerOptions, cancellationToken).ConfigureAwait(false);
        var writer = new SqlServerBulkWriter<T>(scope.Connection, scope.Transaction, writerOptions, _clientFactory);
        await writer.WriteAsync(rows, cancellationToken).ConfigureAwait(false);
        _telemetry.RecordBulkResult(activity, rows.Count);
        activity?.Dispose();
        return new BulkExecutionResult(rows.Count, 0, 0);
    }

    private static SqlBulkCopyOptions BuildSqlBulkCopyOptions(BulkOptions options)
    {
        var flags = options.SqlServerOptions ?? SqlBulkCopyOptions.Default;
        if (options.KeepIdentity)
        {
            flags |= SqlBulkCopyOptions.KeepIdentity;
        }

        if (options.UseTableLock)
        {
            flags |= SqlBulkCopyOptions.TableLock;
        }

        return flags;
    }

    private static void EnsureInsertOnly<T>(BulkOperation<T> operation)
    {
        if (operation.Mode != BulkOperationMode.Insert)
        {
            throw new BulkOperationException($"Bulk mode '{operation.Mode}' is not yet supported.");
        }
    }
}
