using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.InteropServices;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Helpers that project <see cref="DataTable"/> or <see cref="DataSet"/> rows into strongly typed records.
/// </summary>
public static class DataTableMappingExtensions
{
    /// <summary>
    /// Maps every row in the table to <typeparamref name="T"/> using the configured <see cref="IRowMapperFactory"/>.
    /// </summary>
    /// <typeparam name="T">Destination type.</typeparam>
    /// <param name="table">Materialized table.</param>
    /// <param name="mapperFactory">Factory that resolves the optimal mapper.</param>
    /// <param name="mapperRequest">Optional overrides (column map, strategy).</param>
    /// <returns>Projected rows.</returns>
    public static IReadOnlyList<T> MapRows<T>(
        this DataTable table,
        IRowMapperFactory mapperFactory,
        RowMapperRequest? mapperRequest = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(mapperFactory);

        var mapper = mapperFactory.Create<T>(mapperRequest);
        return MapRowsInternal(table, mapper);
    }

    /// <summary>
    /// Maps the specified table within a <see cref="DataSet"/> to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Destination type.</typeparam>
    /// <param name="dataSet">DataSet that holds the materialized tables.</param>
    /// <param name="tableName">Target table name.</param>
    /// <param name="mapperFactory">Factory that resolves the optimal mapper.</param>
    /// <param name="mapperRequest">Optional overrides (column map, strategy).</param>
    /// <returns>Projected rows.</returns>
    public static IReadOnlyList<T> MapRows<T>(
        this DataSet dataSet,
        string tableName,
        IRowMapperFactory mapperFactory,
        RowMapperRequest? mapperRequest = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(mapperFactory);

        var table = dataSet.Tables[tableName];
        if (table is null)
        {
            throw new ArgumentException($"Table '{tableName}' was not found in the DataSet.", nameof(tableName));
        }

        return table.MapRows<T>(mapperFactory, mapperRequest);
    }

    /// <summary>
    /// Maps the table at the specified index within a <see cref="DataSet"/> to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Destination type.</typeparam>
    /// <param name="dataSet">DataSet that holds the materialized tables.</param>
    /// <param name="tableIndex">Zero-based table index.</param>
    /// <param name="mapperFactory">Factory that resolves the optimal mapper.</param>
    /// <param name="mapperRequest">Optional overrides (column map, strategy).</param>
    /// <returns>Projected rows.</returns>
    public static IReadOnlyList<T> MapRows<T>(
        this DataSet dataSet,
        int tableIndex,
        IRowMapperFactory mapperFactory,
        RowMapperRequest? mapperRequest = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentNullException.ThrowIfNull(mapperFactory);
        ArgumentOutOfRangeException.ThrowIfNegative(tableIndex);

        if (tableIndex >= dataSet.Tables.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(tableIndex), tableIndex, "Table index is outside the bounds of the DataSet.");
        }

        return dataSet.Tables[tableIndex]!.MapRows<T>(mapperFactory, mapperRequest);
    }

    private static IReadOnlyList<T> MapRowsInternal<T>(DataTable table, IRowMapper<T> mapper)
        where T : class
    {
        var rowCount = table.Rows.Count;
        if (rowCount == 0)
        {
            return Array.Empty<T>();
        }

        var buffer = GC.AllocateUninitializedArray<T>(rowCount);
        var span = MemoryMarshal.CreateSpan(ref buffer[0], buffer.Length);
        var index = 0;

        using var reader = table.CreateDataReader();
        while (reader.Read())
        {
            span[index++] = mapper.Map(reader);
        }

        if (index == buffer.Length)
        {
            return buffer;
        }

        var trimmed = GC.AllocateUninitializedArray<T>(index);
        var trimmedSpan = MemoryMarshal.CreateSpan(ref trimmed[0], trimmed.Length);
        span[..index].CopyTo(trimmedSpan);
        return trimmed;
    }
}
