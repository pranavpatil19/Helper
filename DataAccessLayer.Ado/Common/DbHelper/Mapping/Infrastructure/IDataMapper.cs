using System.Data.Common;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Maps the current <see cref="DbDataReader"/> row to <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Destination type.</typeparam>
public interface IDataMapper<out T>
{
    /// <summary>
    /// Materializes the current row into <typeparamref name="T"/>.
    /// </summary>
    /// <param name="reader">Active reader positioned on the desired row.</param>
    /// <returns>Materialized instance.</returns>
    T Map(DbDataReader reader);
}
