using DataAccessLayer.Execution;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Factory that produces row mappers with optional per-request overrides.
/// </summary>
/// <remarks>
/// Instantiated and consumed by the top-level query helpers.
/// </remarks>
public interface IRowMapperFactory
{
    /// <summary>
    /// Creates a mapper for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Destination type.</typeparam>
    /// <param name="request">Optional overrides (strategy, casing, column map).</param>
    /// <returns>A mapper capable of projecting <see cref="System.Data.Common.DbDataReader"/> rows to <typeparamref name="T"/>.</returns>
    IRowMapper<T> Create<T>(RowMapperRequest? request = null) where T : class;
}
