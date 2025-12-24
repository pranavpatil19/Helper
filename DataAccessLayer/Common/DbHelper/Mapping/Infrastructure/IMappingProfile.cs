using System;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Defines custom conversions applied while mapping columns to DTO properties.
/// </summary>
public interface IMappingProfile
{
    /// <summary>
    /// Attempts to convert the source value for the specified column/property into the desired target type.
    /// </summary>
    /// <param name="columnName">Database column name (post-column map).</param>
    /// <param name="propertyName">Destination property name.</param>
    /// <param name="targetType">Type of the destination property.</param>
    /// <param name="sourceValue">Original value read from the data source.</param>
    /// <param name="destinationValue">Converted value when the profile handles the conversion.</param>
    /// <returns><c>true</c> when the conversion was handled; otherwise <c>false</c>.</returns>
    bool TryConvert(string columnName, string propertyName, Type targetType, object? sourceValue, out object? destinationValue);
}
