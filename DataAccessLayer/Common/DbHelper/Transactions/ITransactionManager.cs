using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Shared.Configuration;

namespace DataAccessLayer.Transactions;

/// <summary>
/// Coordinates the creation of database transactions with provider-specific savepoint support.
/// </summary>
public interface ITransactionManager
{
    /// <summary>
    /// Begins a new transaction asynchronously.
    /// </summary>
    /// <param name="isolationLevel">Isolation level to use for the transaction.</param>
    /// <param name="options">Optional database options that override the defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<ITransactionScope> BeginAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        DatabaseOptions? options = null,
        TransactionScopeOption scopeOption = TransactionScopeOption.Required,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <param name="isolationLevel">Isolation level to use for the transaction.</param>
    /// <param name="options">Optional database options that override the defaults.</param>
    ITransactionScope Begin(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        DatabaseOptions? options = null,
        TransactionScopeOption scopeOption = TransactionScopeOption.Required);
}
