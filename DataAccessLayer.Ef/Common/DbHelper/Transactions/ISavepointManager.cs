using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Shared.Configuration;

namespace DataAccessLayer.Transactions;

/// <summary>
/// Issues provider-specific commands to manage savepoints inside an existing transaction.
/// </summary>
public interface ISavepointManager
{
    Task BeginSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default);
    void BeginSavepoint(DbTransaction transaction, string name, DatabaseOptions options);
    Task RollbackToSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default);
    void RollbackToSavepoint(DbTransaction transaction, string name, DatabaseOptions options);
    Task ReleaseSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default);
    void ReleaseSavepoint(DbTransaction transaction, string name, DatabaseOptions options);
}
