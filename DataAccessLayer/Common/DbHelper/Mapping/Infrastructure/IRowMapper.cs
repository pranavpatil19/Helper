using System.Data.Common;
using DataAccessLayer.Execution;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Maps a single <see cref="DbDataReader"/> row into <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Destination type.</typeparam>
/// <remarks>
/// Invoked by the row-mapper pipeline inside the top-level query helpers.
/// </remarks>
public interface IRowMapper<out T>
{
    /// <summary>
    /// Materializes the current reader row into <typeparamref name="T"/>.
    /// </summary>
    /// <param name="reader">Active reader positioned on the desired row.</param>
    /// <returns>The mapped instance.</returns>
    T Map(DbDataReader reader);
}
