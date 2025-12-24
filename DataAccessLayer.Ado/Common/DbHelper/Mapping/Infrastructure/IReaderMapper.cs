using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Provides advanced reader-to-object mapping strategies (reflection, IL-emit, source generation).
/// </summary>
/// <typeparam name="T">Destination type.</typeparam>
public interface IReaderMapper<T>
{
    /// <summary>
    /// Reads all rows synchronously.
    /// </summary>
    T[] MapAll(DbDataReader reader);

    /// <summary>
    /// Reads all rows asynchronously.
    /// </summary>
    Task<T[]> MapAllAsync(DbDataReader reader, CancellationToken cancellationToken = default);
}
