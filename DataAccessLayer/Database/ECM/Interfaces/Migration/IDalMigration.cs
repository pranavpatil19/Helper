using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Database.ECM.Interfaces.Migration;

/// <summary>
/// Minimal contract for DAL-driven migration helpers that bootstrap provider-specific artifacts.
/// </summary>
public interface IDalMigration
{
    /// <summary>
    /// Creates (or recreates) the staging table required for batch migrations.
    /// Implementations should wrap the procedure invocation in a transaction.
    /// </summary>
    Task CreateStagingTableAsync(CancellationToken cancellationToken = default);
}
