using System;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Converts numeric or string values to enum targets so DTO properties map cleanly from SQL columns.
/// </summary>
public sealed class EnumMappingProfile : IMappingProfile
{
    public bool TryConvert(string columnName, string propertyName, Type targetType, object? sourceValue, out object? destinationValue)
    {
        destinationValue = null;

        if (!targetType.IsEnum || sourceValue is null || sourceValue is DBNull)
        {
            return false;
        }

        try
        {
            switch (sourceValue)
            {
                case string s:
                    destinationValue = Enum.Parse(targetType, s, ignoreCase: true);
                    return true;
                case byte or short or int or long or sbyte or ushort or uint or ulong:
                    destinationValue = Enum.ToObject(targetType, sourceValue);
                    return true;
                case decimal decimalValue:
                    destinationValue = Enum.ToObject(targetType, (long)decimalValue);
                    return true;
                case double doubleValue:
                    destinationValue = Enum.ToObject(targetType, (long)doubleValue);
                    return true;
            }
        }
        catch
        {
            destinationValue = null;
            return false;
        }

        return false;
    }
}
