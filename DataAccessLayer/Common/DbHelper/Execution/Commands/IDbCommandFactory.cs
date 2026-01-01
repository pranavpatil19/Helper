using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Execution;

/// <summary>
/// Provides an extensibility point for creating <see cref="DbCommand"/> instances from a <see cref="DbCommandRequest"/>.
/// </summary>
/// <remarks>
/// Downstream code typically builds requests with <see cref="DataAccessLayer.Execution.Builders.DbParameter"/> so every parameter includes
/// an explicit <see cref="System.Data.DbType"/> before getting commands.
/// </remarks>
public interface IDbCommandFactory
{
    /// <summary>
    /// Gets a pooled <see cref="DbCommand"/> configured for the given request.
    /// </summary>
    DbCommand GetCommand(DbConnection connection, DbCommandRequest request);

    /// <summary>
    /// Gets a pooled <see cref="DbCommand"/> configured for the given request.
    /// </summary>
    Task<DbCommand> GetCommandAsync(
        DbConnection connection,
        DbCommandRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the command to the underlying pool.
    /// </summary>
    void ReturnCommand(DbCommand command);
}
