using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Execution;

/// <summary>
/// Provides an extensibility point for creating <see cref="DbCommand"/> instances from a <see cref="DbCommandRequest"/>.
/// </summary>
public interface IDbCommandFactory
{
    /// <summary>
    /// Rents a pooled <see cref="DbCommand"/> configured for the given request.
    /// </summary>
    DbCommand Rent(DbConnection connection, DbCommandRequest request);

    /// <summary>
    /// Rents a pooled <see cref="DbCommand"/> configured for the given request.
    /// </summary>
    Task<DbCommand> RentAsync(
        DbConnection connection,
        DbCommandRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the command to the underlying pool.
    /// </summary>
    void Return(DbCommand command);
}
