using System;
using DataAccessLayer.Validation;
using Shared.Configuration;

namespace DataAccessLayer.Execution;

/// <summary>
/// Provides canonical conversions for parameter values before they are handed to providers.
/// </summary>
internal static class ValueNormalizer
{
    public static object? Normalize(DbParameterDefinition definition, object? value, InputNormalizationOptions normalizationOptions)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            DateTime dateTime => NormalizeDateTime(definition, dateTime, normalizationOptions),
            DateTimeOffset dateTimeOffset => NormalizeDateTimeOffset(definition, dateTimeOffset, normalizationOptions),
            DateOnly dateOnly => NormalizeDateOnly(definition, dateOnly, normalizationOptions),
            TimeOnly timeOnly => NormalizeTimeOnly(timeOnly),
            decimal decimalValue => NormalizeDecimal(definition, decimalValue, normalizationOptions),
            object?[] array => NormalizeArray(definition, array, normalizationOptions),
            _ => value
        };
    }

    private static object?[] NormalizeArray(DbParameterDefinition definition, object?[] values, InputNormalizationOptions options)
    {
        if (values.Length == 0)
        {
            return Array.Empty<object?>();
        }

        var normalized = GC.AllocateUninitializedArray<object?>(values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            normalized[i] = Normalize(definition, values[i], options);
        }

        return normalized;
    }

    private static DateTime NormalizeDateTime(DbParameterDefinition definition, DateTime value, InputNormalizationOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        var violation = InputNormalizationInspector.ValidateDate(definition, utc, options);
        if (violation is not null)
        {
            throw new ArgumentOutOfRangeException(definition.Name, utc, violation);
        }

        return utc;
    }

    private static DateTime NormalizeDateTimeOffset(DbParameterDefinition definition, DateTimeOffset value, InputNormalizationOptions options) =>
        NormalizeDateTime(definition, value.ToUniversalTime().DateTime, options);

    private static DateTime NormalizeDateOnly(DbParameterDefinition definition, DateOnly value, InputNormalizationOptions options)
        => NormalizeDateTime(definition, value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), options);

    private static TimeSpan NormalizeTimeOnly(TimeOnly value) => value.ToTimeSpan();

    private static decimal NormalizeDecimal(DbParameterDefinition definition, decimal value, InputNormalizationOptions options)
    {
        if (definition.Scale is { } scale)
        {
            value = Math.Round(value, scale, options.DecimalRoundingStrategy);
        }

        var violation = InputNormalizationInspector.ValidateDecimal(definition, value, options);
        if (violation is not null)
        {
            throw new ArgumentOutOfRangeException(definition.Name, value, violation);
        }

        return value;
    }
}
