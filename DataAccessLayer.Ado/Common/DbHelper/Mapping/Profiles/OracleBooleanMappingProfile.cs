using System;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Converts Oracle boolean representations (NUMBER(1), CHAR(1), Y/N) to CLR bool values.
/// </summary>
public sealed class OracleBooleanMappingProfile : IMappingProfile
{
    public bool TryConvert(string columnName, string propertyName, Type targetType, object? sourceValue, out object? destinationValue)
    {
        destinationValue = null;

        if (targetType != typeof(bool))
        {
            return false;
        }

        if (sourceValue is null || sourceValue is DBNull)
        {
            return false;
        }

        if (sourceValue is bool b)
        {
            destinationValue = b;
            return true;
        }

        switch (sourceValue)
        {
            case char ch:
                if (TryFromString(ch.ToString(), out var boolFromChar))
                {
                    destinationValue = boolFromChar;
                    return true;
                }
                break;
            case string s:
                if (TryFromString(s, out var boolFromString))
                {
                    destinationValue = boolFromString;
                    return true;
                }
                break;
            case decimal decimalValue:
                destinationValue = decimalValue != 0;
                return true;
            case int intValue:
                destinationValue = intValue != 0;
                return true;
            case long longValue:
                destinationValue = longValue != 0;
                return true;
            case short shortValue:
                destinationValue = shortValue != 0;
                return true;
            case byte byteValue:
                destinationValue = byteValue != 0;
                return true;
            case double doubleValue:
                destinationValue = Math.Abs(doubleValue) > double.Epsilon;
                return true;
            case float floatValue:
                destinationValue = Math.Abs(floatValue) > float.Epsilon;
                return true;
        }

        destinationValue = null;
        return false;
    }

    private static bool TryFromString(string value, out bool result)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            result = false;
            return false;
        }

        if (string.Equals(trimmed, "Y", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "TRUE", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (string.Equals(trimmed, "N", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "FALSE", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        if (int.TryParse(trimmed, out var numeric))
        {
            result = numeric != 0;
            return true;
        }

        result = false;
        return false;
    }
}
