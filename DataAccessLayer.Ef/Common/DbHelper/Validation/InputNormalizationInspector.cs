using System;
using DataAccessLayer.Execution;
using Shared.Configuration;

namespace DataAccessLayer.Validation;

/// <summary>
/// Shared validation helpers for date and numeric normalization.
/// </summary>
internal static class InputNormalizationInspector
{
    public static string? ValidateDate(DbParameterDefinition definition, DateTime utcValue, InputNormalizationOptions options)
    {
        if (!options.EnforceDateRange)
        {
            return null;
        }

        if (utcValue < options.MinDateUtc || utcValue > options.MaxDateUtc)
        {
            return $"Parameter '{definition.Name}' value '{utcValue:O}' must be between {options.MinDateUtc:O} and {options.MaxDateUtc:O}.";
        }

        return null;
    }

    public static string? ValidateDecimal(DbParameterDefinition definition, decimal value, InputNormalizationOptions options)
    {
        if (!options.EnforceDecimalPrecision || definition.Precision is not { } precision)
        {
            return null;
        }

        var scale = definition.Scale ?? 0;
        var integerDigitsAllowed = precision - scale;
        if (integerDigitsAllowed < 0)
        {
            return $"Parameter '{definition.Name}' precision ({precision}) must be greater than or equal to scale ({scale}).";
        }

        var integralDigits = CountIntegralDigits(value);
        if (integralDigits > integerDigitsAllowed)
        {
            return $"Parameter '{definition.Name}' value '{value}' exceeds precision {precision} with scale {scale}.";
        }

        return null;
    }

    private static int CountIntegralDigits(decimal value)
    {
        var integral = decimal.Truncate(decimal.Abs(value));
        if (integral == 0)
        {
            return 1;
        }

        var digits = 0;
        while (integral >= 1)
        {
            integral = decimal.Truncate(integral / 10m);
            digits++;
        }

        return digits;
    }
}
