using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;
using DataAccessLayer.Mapping;
using Shared.IO;

namespace DataAccessLayer.Common.DbHelper;

public sealed partial class DatabaseHelper
{
    /// <summary>
    /// Streams rows asynchronously without buffering the entire result set.
    /// </summary>
    /// <typeparam name="T">Type produced by the mapper.</typeparam>
    /// <param name="request">Command request describing text, parameters, and transaction context.</param>
    /// <param name="mapper">Projection delegate invoked per row.</param>
    /// <param name="cancellationToken">Propagation token for the async operation.</param>
    /// <returns>IAsyncEnumerable that lazily yields records.</returns>
    public IAsyncEnumerable<T> StreamAsync<T>(
        DbCommandRequest request,
        Func<DbDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ValidateRequest(request);
        return StreamAsyncCore(request, mapper, cancellationToken);

        async IAsyncEnumerable<T> StreamAsyncCore(
            DbCommandRequest innerRequest,
            Func<DbDataReader, T> innerMapper,
            [EnumeratorCancellation] CancellationToken innerToken)
        {
            await using var scope = await AcquireConnectionScopeAsync(innerRequest, innerToken).ConfigureAwait(false);
            var command = await _commandFactory.GetCommandAsync(scope.Connection, innerRequest, innerToken).ConfigureAwait(false);
            ApplyScopedTransaction(innerRequest, scope, command);
            var activity = StartActivity(nameof(StreamAsync), innerRequest);
            try
            {
                var behavior = DbStreamUtilities.EnsureSequentialBehavior(innerRequest.CommandBehavior);

                await using var reader = await ExecuteWithResilienceAsync(
                    token => ExecuteReaderWithFallbackAsync(innerRequest, command, behavior, token).AsTask(),
                    innerToken).ConfigureAwait(false);
                var yielded = 0;
                try
                {
                    while (await reader.ReadAsync(innerToken).ConfigureAwait(false))
                    {
                        yielded++;
                        yield return innerMapper(reader);
                    }
                }
                finally
                {
                    var execution = new DbExecutionResult(reader.RecordsAffected, null, EmptyOutputs);
                    _telemetry.RecordCommandResult(activity, execution, yielded);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
            }
            finally
            {
                _commandFactory.ReturnCommand(command);
                activity?.Dispose();
            }
        }
    }

    /// <summary>
    /// Streams rows asynchronously using the configured row mapper.
    /// </summary>
    public IAsyncEnumerable<T> StreamAsync<T>(
        DbCommandRequest request,
        RowMapperRequest? mapperRequest = null,
        CancellationToken cancellationToken = default)
        where T : class =>
        StreamAsync(request, ResolveRowMapper<T>(mapperRequest), cancellationToken);

    /// <summary>
    /// Streams binary data from a single column into the specified stream asynchronously.
    /// </summary>
    public Task<long> StreamColumnAsync(
        DbCommandRequest request,
        int ordinal,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return WrapExecutionAsync(request, nameof(StreamColumnAsync), () =>
            ExecuteWithResilienceAsync(
                token => StreamBinaryInternalAsync(request, ordinal, destination, token),
                cancellationToken),
            (activity, bytes) => activity?.SetTag("db.stream.bytes", bytes));
    }

    /// <summary>
    /// Streams binary data from a single column synchronously.
    /// </summary>
    public long StreamColumn(DbCommandRequest request, int ordinal, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return WrapExecution(request, nameof(StreamColumn), () => ExecuteWithResilience(() => StreamBinaryInternal(request, ordinal, destination)),
            (activity, bytes) => activity?.SetTag("db.stream.bytes", bytes));
    }

    /// <summary>
    /// Streams text data from a single column into the specified writer asynchronously.
    /// </summary>
    public Task<long> StreamTextAsync(
        DbCommandRequest request,
        int ordinal,
        TextWriter writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return WrapExecutionAsync(request, nameof(StreamTextAsync), () =>
            ExecuteWithResilienceAsync(
                token => StreamTextInternalAsync(request, ordinal, writer, token),
                cancellationToken),
            (activity, chars) => activity?.SetTag("db.stream.chars", chars));
    }

    /// <summary>
    /// Streams text data from a single column synchronously.
    /// </summary>
    public long StreamText(DbCommandRequest request, int ordinal, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return WrapExecution(request, nameof(StreamText), () => ExecuteWithResilience(() => StreamTextInternal(request, ordinal, writer)),
            (activity, chars) => activity?.SetTag("db.stream.chars", chars));
    }
}
