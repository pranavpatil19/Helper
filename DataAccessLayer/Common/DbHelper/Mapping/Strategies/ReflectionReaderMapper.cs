using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Reader mapper that materializes all rows using the <see cref="ReflectionDataMapper{T}"/>.
/// </summary>
/// <typeparam name="T">Destination type.</typeparam>
public sealed class ReflectionReaderMapper<T> : IReaderMapper<T>
    where T : class, new()
{
    private readonly ReflectionDataMapper<T> _mapper;

    public ReflectionReaderMapper(bool ignoreCase = true)
    {
        _mapper = new ReflectionDataMapper<T>(ignoreCase);
    }

    public T[] MapAll(DbDataReader reader)
    {
        var list = new List<T>();
        while (reader.Read())
        {
            list.Add(_mapper.Map(reader));
        }

        return list.ToArray();
    }

    public async Task<T[]> MapAllAsync(DbDataReader reader, CancellationToken cancellationToken = default)
    {
        var list = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(_mapper.Map(reader));
        }

        return list.ToArray();
    }
}
