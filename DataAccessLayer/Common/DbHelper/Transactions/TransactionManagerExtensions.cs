using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Shared.Configuration;

namespace DataAccessLayer.Transactions;

/// <summary>
/// Convenience helpers for running work inside <see cref="ITransactionManager"/> while centralizing commit/rollback handling.
/// </summary>
public static class TransactionManagerExtensions
{
    public static Task WithTransactionAsync(
        this ITransactionManager manager,
        Func<ITransactionScope, CancellationToken, Task> work,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        DatabaseOptions? options = null,
        TransactionScopeOption scopeOption = TransactionScopeOption.Required,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(work);

        return manager.WithTransactionAsync<object?>(
            async (scope, token) =>
            {
                await work(scope, token).ConfigureAwait(false);
                return null;
            },
            isolationLevel,
            options,
            scopeOption,
            cancellationToken);
    }

    public static async Task<TResult> WithTransactionAsync<TResult>(
        this ITransactionManager manager,
        Func<ITransactionScope, CancellationToken, Task<TResult>> work,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        DatabaseOptions? options = null,
        TransactionScopeOption scopeOption = TransactionScopeOption.Required,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(work);

        await using var scope = await manager.BeginAsync(
            isolationLevel,
            options,
            scopeOption,
            cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await work(scope, cancellationToken).ConfigureAwait(false);
            await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await scope.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
