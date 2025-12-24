using System;
using System.Collections.Concurrent;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Normalizes PostgreSQL snake_case column names into PascalCase to match CLR property names.
/// </summary>
public sealed class PostgresSnakeCaseColumnNameNormalizer : IColumnNameNormalizer
{
    private static readonly ConcurrentDictionary<string, string> Cache = new();

    public string Normalize(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return columnName;
        }

        return Cache.GetOrAdd(columnName, static name => ConvertToPascalCase(name));
    }

    private static string ConvertToPascalCase(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var index = 0;
        var capitalize = true;

        foreach (var ch in value)
        {
            if (ch == '_')
            {
                capitalize = true;
                continue;
            }

            var c = capitalize ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch);
            buffer[index++] = c;
            capitalize = false;
        }

        return new string(buffer[..index]);
    }
}
