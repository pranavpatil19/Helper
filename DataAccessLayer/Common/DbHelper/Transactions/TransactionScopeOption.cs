namespace DataAccessLayer.Transactions;

/// <summary>
/// Mirrors the semantics of <see cref="System.Transactions.TransactionScopeOption"/>.
/// </summary>
public enum TransactionScopeOption
{
    Required = 0,
    RequiresNew = 1,
    Suppress = 2
}
