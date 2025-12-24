using System;

namespace DataAccessLayer.Execution;

/// <summary>
/// Represents the data returned by <see cref="IDatabaseHelper"/> query helpers plus execution metadata.
/// </summary>
/// <typeparam name="T">Materialized payload type.</typeparam>
/// <remarks>
/// Returned by the top-level query helpers on <see cref="IDatabaseHelper"/> (e.g., <see cref="IDatabaseHelper.QueryAsync{T}(DbCommandRequest, System.Func{System.Data.Common.DbDataReader, T}, System.Threading.CancellationToken)"/>,
/// <see cref="IDatabaseHelper.Query{T}(DbCommandRequest, System.Func{System.Data.Common.DbDataReader, T})"/>,
/// <see cref="IDatabaseHelper.LoadDataTableAsync"/> and <see cref="IDatabaseHelper.LoadDataTable"/>).
/// </remarks>
public sealed class DbQueryResult<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DbQueryResult{T}"/> class.
    /// </summary>
    /// <param name="data">Materialized payload.</param>
    /// <param name="execution">Execution metadata (rows affected, OUT params).</param>
    public DbQueryResult(T data, DbExecutionResult execution)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Execution = execution ?? throw new ArgumentNullException(nameof(execution));
    }

    /// <summary>
    /// Gets the materialized payload.
    /// </summary>
    public T Data { get; }

    /// <summary>
    /// Gets the execution metadata associated with the query.
    /// </summary>
    public DbExecutionResult Execution { get; }
}
