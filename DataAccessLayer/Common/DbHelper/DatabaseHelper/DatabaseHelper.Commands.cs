using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;

namespace DataAccessLayer.Common.DbHelper;

public sealed partial class DatabaseHelper
{
    /// <summary>
    /// Executes a non-query command asynchronously and returns rows affected plus output parameters.
    /// </summary>
    /// <param name="request">Command request describing text, parameters, and transaction context.</param>
    /// <param name="cancellationToken">Propagation token for the async operation.</param>
    /// <returns>Rows affected plus provider-specific output parameters.</returns>
    public Task<DbExecutionResult> ExecuteAsync(DbCommandRequest request, CancellationToken cancellationToken = default) =>
        ExecuteWithRequestActivityAsync(
            nameof(ExecuteAsync),
            request,
            async () =>
            {
                if (TryExecutePostgresOutParametersAsync(request, expectScalar: false, cancellationToken, out var task))
                {
                    return await task.ConfigureAwait(false);
                }

                return await ExecuteWithResilienceAsync(
                    token => ExecuteScalarResultAsync(
                        request,
                        async (command, innerToken) =>
                        {
                            var rows = await ExecuteNonQueryWithFallbackAsync(request, command, innerToken).ConfigureAwait(false);
                            return new CommandResult<object?>(null, rows);
                        },
                        token),
                    cancellationToken).ConfigureAwait(false);
            },
            RecordActivityResult);

    /// <summary>
    /// Executes a non-query synchronously and returns rows affected plus output parameters.
    /// </summary>
    /// <param name="request">Command request describing text, parameters, and transaction context.</param>
    /// <returns>Rows affected plus provider-specific output parameters.</returns>
    public DbExecutionResult Execute(DbCommandRequest request) =>
        ExecuteWithRequestActivity(
            nameof(Execute),
            request,
            () =>
            {
                if (TryExecutePostgresOutParameters(request, expectScalar: false, out var result))
                {
                    return result;
                }

                return ExecuteWithResilience(() => ExecuteScalarResult(request, command =>
                {
                    var rows = command.ExecuteNonQuery();
                    return new CommandResult<object?>(null, rows);
                }));
            },
            RecordActivityResult);

    /// <summary>
    /// Executes a scalar query asynchronously.
    /// </summary>
    /// <param name="request">Command request describing text, parameters, and transaction context.</param>
    /// <param name="cancellationToken">Propagation token for the async operation.</param>
    /// <returns>The scalar value plus output parameters.</returns>
    public Task<DbExecutionResult> ExecuteScalarAsync(DbCommandRequest request, CancellationToken cancellationToken = default) =>
        ExecuteWithRequestActivityAsync(
            nameof(ExecuteScalarAsync),
            request,
            async () =>
            {
                if (TryExecutePostgresOutParametersAsync(request, expectScalar: true, cancellationToken, out var task))
                {
                    return await task.ConfigureAwait(false);
                }

                return await ExecuteWithResilienceAsync(
                    token => ExecuteScalarResultAsync(
                        request,
                        async (command, innerToken) =>
                        {
                            var scalar = await ExecuteScalarWithFallbackAsync(request, command, innerToken).ConfigureAwait(false);
                            return new CommandResult<object?>(scalar, -1);
                        },
                        token),
                    cancellationToken).ConfigureAwait(false);
            },
            RecordActivityResult);

    /// <summary>
    /// Executes a scalar query synchronously.
    /// </summary>
    /// <param name="request">Command request describing text, parameters, and transaction context.</param>
    /// <returns>The scalar value plus output parameters.</returns>
    public DbExecutionResult ExecuteScalar(DbCommandRequest request) =>
        ExecuteWithRequestActivity(
            nameof(ExecuteScalar),
            request,
            () =>
            {
                if (TryExecutePostgresOutParameters(request, expectScalar: true, out var result))
                {
                    return result;
                }

                return ExecuteWithResilience(() => ExecuteScalarResult(request, command =>
                {
                    var scalar = command.ExecuteScalar();
                    return new CommandResult<object?>(scalar, -1);
                }));
            },
            RecordActivityResult);
}
