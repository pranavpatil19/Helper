using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using DataAccessLayer.Execution;
using DataAccessLayer.Common.DbHelper;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using DataAccessLayer.Transactions;
using DataAccessLayer.Exceptions;
using Microsoft.Data.SqlClient;

namespace DataAccessLayer.EF;

/// <summary>
/// EF Core extensions that delegate to DAL bulk writers.
/// </summary>
public static class BulkExtensions
{
    public static Task WriteSqlServerBulkAsync<T>(
        this DbContext context,
        IEnumerable<T> rows,
        SqlServerBulkWriterOptions<T> options,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);

        var factory = services.GetRequiredService<IDbConnectionFactory>();
        var dbOptions = services.GetRequiredService<DatabaseOptions>();
        var clientFactory = services.GetRequiredService<ISqlBulkCopyClientFactory>();

        var writer = new SqlServerBulkWriter<T>(factory, dbOptions, options, clientFactory);
        return writer.WriteAsync(rows, cancellationToken);
    }

    /// <summary>
    /// Executes a DAL bulk operation using the current DbContext connection/transaction when possible,
    /// falling back to IBulkWriteHelper when provider overrides are supplied.
    /// </summary>
    public static async Task<BulkExecutionResult> WriteBulkAsync<T>(
        this DbContext context,
        BulkOperation<T> operation,
        IEnumerable<T> rows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(rows);

        var materialized = rows as IReadOnlyList<T> ?? rows.ToList();
        if (materialized.Count == 0)
        {
            return BulkExecutionResult.Empty;
        }

        var services = ((IInfrastructure<IServiceProvider>)context).Instance;
        var bulkHelper = services.GetRequiredService<IBulkWriteHelper>();

        // If callers explicitly override provider/connection, defer to the shared helper path.
        if (operation.Options.OverrideOptions is not null)
        {
            return await bulkHelper.ExecuteAsync(operation, materialized, cancellationToken).ConfigureAwait(false);
        }

        // Enforce transaction requirement either via ambient scope or DbContext transaction.
        if (operation.Options.RequireAmbientTransaction &&
            TransactionScopeAmbient.Current is null &&
            context.Database.CurrentTransaction is null)
        {
            throw new BulkOperationException("Bulk operation requires an ambient transaction, but none is active.");
        }

        var connection = context.Database.GetDbConnection();
        var currentTransaction = context.Database.CurrentTransaction?.GetDbTransaction();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var providerName = context.Database.ProviderName ?? string.Empty;
            var destinationTable = operation.Options.DestinationTableOverride ?? operation.Mapping.TableName;

            if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                var clientFactory = services.GetRequiredService<ISqlBulkCopyClientFactory>();
                var writerOptions = new SqlServerBulkWriterOptions<T>
                {
                    DestinationTable = destinationTable,
                    ColumnNames = operation.Mapping.Columns.Select(c => c.ColumnName).ToArray(),
                    Columns = operation.Mapping.Columns,
                    ValueSelector = operation.Mapping.ValueSelector,
                    OverrideOptions = operation.Options.OverrideOptions,
                    BulkCopyOptions = BuildSqlBulkCopyOptions(operation.Options),
                    BatchSize = operation.Options.BatchSize,
                    BulkCopyTimeoutSeconds = operation.Options.CommandTimeoutSeconds
                };

                var writer = new SqlServerBulkWriter<T>(connection, currentTransaction, writerOptions, clientFactory);
                await writer.WriteAsync(materialized, cancellationToken).ConfigureAwait(false);
                return new BulkExecutionResult(materialized.Count, 0, 0);
            }

            if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
                providerName.Contains("Postgre", StringComparison.OrdinalIgnoreCase))
            {
                var clientFactory = services.GetRequiredService<IPostgresCopyClientFactory>();
                var writerOptions = new PostgresBulkWriterOptions<T>
                {
                    DestinationTable = destinationTable,
                    ColumnNames = operation.Mapping.Columns.Select(c => c.ColumnName).ToArray(),
                    Columns = operation.Mapping.Columns,
                    ValueSelector = operation.Mapping.ValueSelector
                };

                var writer = new PostgresBulkWriter<T>(connection, writerOptions, clientFactory);
                await writer.WriteAsync(materialized, cancellationToken).ConfigureAwait(false);
                return new BulkExecutionResult(materialized.Count, 0, 0);
            }

            if (providerName.Contains("Oracle", StringComparison.OrdinalIgnoreCase))
            {
                var parameterNames = operation.Mapping.Columns
                    .Select((_, index) => $":p{index}")
                    .ToArray();

                var columnList = string.Join(", ", operation.Mapping.Columns.Select(c => c.ColumnName));
                var valuesList = string.Join(", ", parameterNames);
                var commandText = $"INSERT INTO {destinationTable} ({columnList}) VALUES ({valuesList})";

                var writerOptions = new OracleBulkWriterOptions<T>
                {
                    CommandText = commandText,
                    CommandType = CommandType.Text,
                    ParameterNames = parameterNames,
                    ParameterDbTypes = operation.Mapping.Columns.Select(c => c.DbType).ToArray(),
                    BatchSize = operation.Options.BatchSize ?? 256,
                    ValueSelector = operation.Mapping.ValueSelector
                };

                var writer = new OracleBulkWriter<T>(connection, writerOptions);
                await writer.WriteAsync(materialized, cancellationToken).ConfigureAwait(false);
                return new BulkExecutionResult(materialized.Count, 0, 0);
            }
        }
        finally
        {
            if (shouldClose && currentTransaction is null)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }

        // Fallback to helper for unknown providers.
        return await bulkHelper.ExecuteAsync(operation, materialized, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronous counterpart to <see cref="WriteBulkAsync{T}(DbContext,BulkOperation{T},IEnumerable{T},CancellationToken)"/>.
    /// </summary>
    public static BulkExecutionResult WriteBulk<T>(
        this DbContext context,
        BulkOperation<T> operation,
        IEnumerable<T> rows)
    {
        return WriteBulkAsync(context, operation, rows, CancellationToken.None).GetAwaiter().GetResult();
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
}
