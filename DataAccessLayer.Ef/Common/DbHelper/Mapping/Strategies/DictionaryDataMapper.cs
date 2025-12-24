using System;
using System.Collections.Generic;
using System.Data.Common;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Maps the current row into an <see cref="IReadOnlyDictionary{TKey,TValue}"/> keyed by column name.
/// </summary>
public sealed class DictionaryDataMapper : IDataMapper<IReadOnlyDictionary<string, object?>>
{
    private readonly IEqualityComparer<string> _comparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryDataMapper"/> class.
    /// </summary>
    /// <param name="ignoreCase">Whether column names should be compared case-insensitively.</param>
    public DictionaryDataMapper(bool ignoreCase = true)
    {
        _comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Map(DbDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var dictionary = new Dictionary<string, object?>(reader.FieldCount, _comparer);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            dictionary[columnName] = value;
        }

        return dictionary;
    }
}
