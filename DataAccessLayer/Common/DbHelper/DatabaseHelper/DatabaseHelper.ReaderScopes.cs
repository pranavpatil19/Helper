using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;

namespace DataAccessLayer.Common.DbHelper;

public sealed partial class DatabaseHelper
{
    /// <summary>
    /// Executes a reader asynchronously and returns a lease that controls disposal.
    /// </summary>
    public async Task<DbReaderScope> ExecuteReaderAsync(
        DbCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        try
        {
            var scope = await AcquireConnectionScopeAsync(request, cancellationToken).ConfigureAwait(false);
            var command = await _commandFactory.GetCommandAsync(scope.Connection, request, cancellationToken).ConfigureAwait(false);
            ApplyScopedTransaction(request, scope, command);
            var behavior = request.CommandBehavior == CommandBehavior.Default
                ? CommandBehavior.Default
                : request.CommandBehavior;
            var reader = await ExecuteReaderWithFallbackAsync(request, command, behavior, cancellationToken).ConfigureAwait(false);
            return new DbReaderScope(reader, command, scope, _commandFactory);
        }
        catch (Exception ex)
        {
            throw WrapException(request, ex);
        }
    }

    /// <summary>
    /// Executes a reader synchronously and returns a lease that tracks command and connection ownership.
    /// </summary>
    public DbReaderScope ExecuteReader(DbCommandRequest request) =>
        ExecuteWithRequestActivity(
            nameof(ExecuteReader),
            request,
            () =>
            {
                var scope = AcquireConnectionScope(request);
                var command = _commandFactory.GetCommand(scope.Connection, request);
                ApplyScopedTransaction(request, scope, command);
                var behavior = request.CommandBehavior == CommandBehavior.Default
                    ? CommandBehavior.Default
                    : request.CommandBehavior;
                var reader = command.ExecuteReader(behavior);
                return new DbReaderScope(reader, command, scope, _commandFactory);
            });
}
