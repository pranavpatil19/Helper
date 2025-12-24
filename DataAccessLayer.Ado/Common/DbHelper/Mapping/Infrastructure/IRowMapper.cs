using System.Data.Common;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Maps a single <see cref="DbDataReader"/> row into <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Destination type.</typeparam>
public interface IRowMapper<out T>
{
    /// <summary>
    /// Materializes the current reader row into <typeparamref name="T"/>.
    /// </summary>
    /// <param name="reader">Active reader positioned on the desired row.</param>
    /// <returns>The mapped instance.</returns>
    T Map(DbDataReader reader);
}
