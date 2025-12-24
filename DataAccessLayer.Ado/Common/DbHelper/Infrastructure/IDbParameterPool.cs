using System.Data.Common;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Provides DbParameter pooling services to reduce allocations on hot paths.
/// </summary>
public interface IDbParameterPool
{
    /// <summary>
    /// Rents a parameter compatible with the specified command.
    /// </summary>
    DbParameter Rent(DbCommand command);

    /// <summary>
    /// Returns the parameter to the underlying pool (no-op when pooling is disabled).
    /// </summary>
    void Return(DbParameter parameter);

    /// <summary>
    /// Gets a value indicating whether pooling is currently enabled.
    /// </summary>
    bool IsEnabled { get; }
}
