using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Dynamic;
using System.Linq;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Maps the current row into a dynamic (<see cref="ExpandoObject"/>) payload.
/// </summary>
public sealed class DynamicDataMapper : IDataMapper<object>
{
    private readonly bool _ignoreCase;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicDataMapper"/> class.
    /// </summary>
    /// <param name="ignoreCase">Whether column/property names should ignore casing.</param>
    public DynamicDataMapper(bool ignoreCase = true)
    {
        _ignoreCase = ignoreCase;
    }

    /// <inheritdoc />
    public object Map(DbDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        IDictionary<string, object?> expando = new ExpandoObject();
        var comparer = _ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);

            if (_ignoreCase)
            {
                SetCaseInsensitive(expando, columnName, value, comparer);
            }
            else
            {
                expando[columnName] = value;
            }
        }

        return (ExpandoObject)expando;
    }

    private static void SetCaseInsensitive(
        IDictionary<string, object?> expando,
        string columnName,
        object? value,
        IEqualityComparer<string> comparer)
    {
        var existingKey = expando.Keys.FirstOrDefault(key => comparer.Equals(key, columnName));
        if (existingKey is not null)
        {
            expando[existingKey] = value;
            return;
        }

        expando[columnName] = value;
    }
}
