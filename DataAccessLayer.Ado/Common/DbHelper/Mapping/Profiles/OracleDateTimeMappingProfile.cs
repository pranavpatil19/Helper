using System;
using System.Globalization;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Converts Oracle DATE/TIMESTAMP (without timezone) representations to CLR DateTimeOffset/DateTime values.
/// </summary>
public sealed class OracleDateTimeMappingProfile : IMappingProfile
{
    public bool TryConvert(string columnName, string propertyName, Type targetType, object? sourceValue, out object? destinationValue)
    {
        destinationValue = null;
        if (sourceValue is null || sourceValue is DBNull)
        {
            return false;
        }

        if (targetType == typeof(DateTimeOffset))
        {
            if (TryToDateTimeOffset(sourceValue, out var dto))
            {
                destinationValue = dto;
                return true;
            }

            return false;
        }

        if (targetType == typeof(DateTime))
        {
            if (TryToDateTime(sourceValue, out var dt))
            {
                destinationValue = dt;
                return true;
            }

            return false;
        }

        return false;
    }

    private static bool TryToDateTimeOffset(object value, out DateTimeOffset result)
    {
        if (value is DateTime dt)
        {
            result = NormalizeDateTime(dt);
            return true;
        }

        if (value is DateTimeOffset dto)
        {
            result = dto.ToUniversalTime();
            return true;
        }

        if (value is string s && TryParseString(s, out var parsed))
        {
            result = parsed;
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryToDateTime(object value, out DateTime result)
    {
        if (value is DateTime dt)
        {
            result = NormalizeDateTime(dt).UtcDateTime;
            return true;
        }

        if (value is DateTimeOffset dto)
        {
            result = dto.UtcDateTime;
            return true;
        }

        if (value is string s && TryParseString(s, out var parsed))
        {
            result = parsed.UtcDateTime;
            return true;
        }

        result = default;
        return false;
    }

    private static DateTimeOffset NormalizeDateTime(DateTime dt)
    {
        var kind = dt.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : dt.Kind;
        var utc = kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }

    private static bool TryParseString(string value, out DateTimeOffset result)
    {
        var trimmed = value.Trim();
        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
        {
            return true;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            result = NormalizeDateTime(dt);
            return true;
        }

        return false;
    }
}
