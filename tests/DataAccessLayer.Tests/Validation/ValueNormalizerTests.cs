using System;
using System.Globalization;
using DataAccessLayer.Execution;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests.Validation;

public sealed class ValueNormalizerTests
{
    private static readonly InputNormalizationOptions DefaultOptions = new();

    [Fact]
    public void Normalize_RoundsDecimals_ToScale()
    {
        var definition = new DbParameterDefinition
        {
            Scale = 2
        };

        var normalized = ValueNormalizer.Normalize(definition, 123.456m, DefaultOptions);

        Assert.Equal(123.46m, normalized);
    }

    [Fact]
    public void Normalize_ConvertsDateTimeToUtc()
    {
        var definition = new DbParameterDefinition();
        var local = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Local);

        var normalized = (DateTime?)ValueNormalizer.Normalize(definition, local, DefaultOptions);

        Assert.Equal(DateTimeKind.Utc, normalized?.Kind);
    }

    [Fact]
    public void Normalize_Throws_WhenDateOutsideRange()
    {
        var definition = new DbParameterDefinition { Name = "cutoff" };
        var options = new InputNormalizationOptions
        {
            MinDateUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            MaxDateUtc = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ValueNormalizer.Normalize(definition, new DateTime(1999, 12, 31, 0, 0, 0, DateTimeKind.Utc), options));
    }

    [Fact]
    public void Normalize_Throws_WhenDecimalExceedsPrecision()
    {
        var definition = new DbParameterDefinition
        {
            Name = "amount",
            Precision = 5,
            Scale = 2
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ValueNormalizer.Normalize(definition, 1234.56m, DefaultOptions));
    }
}
