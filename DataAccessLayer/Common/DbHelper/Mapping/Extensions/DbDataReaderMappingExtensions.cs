using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using DataAccessLayer.Execution;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Extensions that project an active <see cref="DbDataReader"/> into lists, dictionaries, or <see cref="DataTable"/> instances.
/// </summary>
/// <remarks>
/// Intended to be used with readers obtained from the top-level streaming APIs (<see cref="IDatabaseHelper.ExecuteReader"/> and
/// <see cref="IDatabaseHelper.ExecuteReaderAsync"/>).
/// </remarks>
public static class DbDataReaderMappingExtensions
{
    /// <summary>
    /// Materializes the remaining rows of the reader into a list of <typeparamref name="T"/>.
    /// </summary>
    public static IReadOnlyList<T> MapRows<T>(
        this DbDataReader reader,
        IRowMapperFactory mapperFactory,
        RowMapperRequest? mapperRequest = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(mapperFactory);

        var mapper = mapperFactory.Create<T>(mapperRequest);
        return MapRows(reader, mapper);
    }

    /// <summary>
    /// Materializes the remaining rows of the reader into a list using an explicit mapper instance.
    /// </summary>
    public static IReadOnlyList<T> MapRows<T>(this DbDataReader reader, IRowMapper<T> mapper)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(mapper);

        var items = new List<T>();
        while (reader.Read())
        {
            items.Add(mapper.Map(reader));
        }

        return items.Count == 0
            ? Array.Empty<T>()
            : items.ToArray();
    }

    /// <summary>
    /// Materializes the rows as dictionaries (column name -> value).
    /// </summary>
    /// <param name="reader">Active reader.</param>
    /// <param name="mapperFactory">Factory that resolves dictionary mappers.</param>
    /// <param name="ignoreCase">Controls whether dictionary keys ignore casing.</param>
    /// <returns>One dictionary per row.</returns>
    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> MapDictionaries(
        this DbDataReader reader,
        IRowMapperFactory mapperFactory,
        bool ignoreCase = true)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(mapperFactory);

        var request = new RowMapperRequest
        {
            Strategy = MapperStrategy.Dictionary,
            IgnoreCase = ignoreCase
        };

        return reader.MapRows<IReadOnlyDictionary<string, object?>>(mapperFactory, request);
    }

    /// <summary>
    /// Copies the current result set into a <see cref="DataTable"/>.
    /// </summary>
    public static DataTable ToDataTable(this DbDataReader reader, string? tableName = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var table = string.IsNullOrWhiteSpace(tableName)
            ? new DataTable()
            : new DataTable(tableName);

        table.Load(reader);
        return table;
    }
}
