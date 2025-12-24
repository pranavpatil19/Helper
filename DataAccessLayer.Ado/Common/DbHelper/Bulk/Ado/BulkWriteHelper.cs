using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shared.Configuration;
using DataAccessLayer.Exceptions;

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

        if (!_engines.TryGetValue(providerOptions.Provider, out var engine))
        {
            throw new BulkOperationException($"Provider '{providerOptions.Provider}' is not supported for bulk operations.");
        }

        return await engine.ExecuteAsync(operation, materialized, providerOptions, cancellationToken).ConfigureAwait(false);
    }
}
