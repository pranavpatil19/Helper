using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shared.Configuration;
using DataAccessLayer.Exceptions;
using DataAccessLayer.Transactions;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Coordinates provider-specific bulk writers using shared mappings/options.
/// </summary>
public sealed class BulkWriteHelper : IBulkWriteHelper
{
    private readonly DatabaseOptions _defaultOptions;
    private readonly IReadOnlyDictionary<DatabaseProvider, IBulkEngine> _engines;

    public BulkWriteHelper(
        DatabaseOptions defaultOptions,
        IEnumerable<IBulkEngine> engines)
    {
        _defaultOptions = defaultOptions ?? throw new ArgumentNullException(nameof(defaultOptions));
        if (engines is null)
        {
            throw new ArgumentNullException(nameof(engines));
        }

        var resolved = engines.ToList();
        if (resolved.Count == 0)
        {
            throw new BulkOperationException("No bulk engines have been registered.");
        }

        _engines = resolved.ToDictionary(engine => engine.Provider);
    }

    /// <summary>
    /// Executes the supplied bulk operation asynchronously using the provider-specific engine.
    /// </summary>
    /// <typeparam name="T">Row CLR type the <see cref="BulkOperation{T}"/> maps.</typeparam>
    /// <param name="operation">Bulk operation describing the destination table, column map, and value selectors.</param>
    /// <param name="rows">Rows to persist. The helper materializes enumerables so they can be sent to engines multiple times.</param>
    /// <param name="cancellationToken">Propagation token for the async operation.</param>
    /// <returns>Execution details including rows inserted and any provider metadata.</returns>
    /// <remarks>
    /// If <see cref="BulkOperationOptions.RequireAmbientTransaction"/> is true, callers must establish
    /// an ambient transaction (for example, via <see cref="TransactionScopeAmbient"/>). Otherwise the helper
    /// creates scoped connections per engine and commits immediately.
    /// </remarks>
    public async Task<BulkExecutionResult> ExecuteAsync<T>(
        BulkOperation<T> operation,
        IReadOnlyCollection<T> rows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return BulkExecutionResult.Empty;
        }

        var materialized = rows as IReadOnlyList<T> ?? rows.ToList();
        var providerOptions = operation.Options.OverrideOptions ?? _defaultOptions;

        if (operation.Options.RequireAmbientTransaction && TransactionScopeAmbient.Current is null)
        {
            throw new BulkOperationException("Bulk operation requires an ambient transaction, but none is active.");
        }

        if (!_engines.TryGetValue(providerOptions.Provider, out var engine))
        {
            throw new BulkOperationException($"Provider '{providerOptions.Provider}' is not supported for bulk operations.");
        }

        return await engine.ExecuteAsync(operation, materialized, providerOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the supplied bulk operation synchronously.
    /// </summary>
    /// <typeparam name="T">Row CLR type the <see cref="BulkOperation{T}"/> maps.</typeparam>
    /// <param name="operation">Bulk operation describing the destination table, column map, and value selectors.</param>
    /// <param name="rows">Rows to persist.</param>
    /// <returns>Execution details including rows inserted and any provider metadata.</returns>
    public BulkExecutionResult Execute<T>(
        BulkOperation<T> operation,
        IReadOnlyCollection<T> rows)
    {
        return ExecuteAsync(operation, rows, CancellationToken.None).GetAwaiter().GetResult();
    }
}
