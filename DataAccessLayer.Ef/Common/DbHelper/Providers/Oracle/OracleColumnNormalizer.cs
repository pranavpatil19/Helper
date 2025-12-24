using System;
using System.Collections.Generic;

namespace DataAccessLayer.Providers.Oracle;

/// <summary>
/// Normalizes Oracle column names to uppercase for dictionary-based mapping.
/// </summary>
public static class OracleColumnNormalizer
{
    public static IReadOnlyDictionary<string, object?> Normalize(IReadOnlyDictionary<string, object?> row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var result = new Dictionary<string, object?>(row.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in row)
        {
            result[key?.ToUpperInvariant() ?? string.Empty] = value;
        }

        return result;
    }
}
