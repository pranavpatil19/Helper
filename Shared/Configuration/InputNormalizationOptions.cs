using System;

namespace Shared.Configuration;

/// <summary>
/// Configures how date/time and numeric inputs are normalized before reaching the database.
/// </summary>
public sealed class InputNormalizationOptions
{
    /// <summary>
    /// Gets or sets the minimum allowed UTC date. Values earlier than this trigger validation failures.
    /// </summary>
    public DateTime MinDateUtc { get; init; } = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Gets or sets the maximum allowed UTC date. Values later than this trigger validation failures.
    /// </summary>
    public DateTime MaxDateUtc { get; init; } = new(2100, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    /// <summary>
    /// Gets or sets a value indicating whether the DAL enforces the configured UTC range.
    /// </summary>
    public bool EnforceDateRange { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether decimal precision/scale should be validated.
    /// </summary>
    public bool EnforceDecimalPrecision { get; init; } = true;

    /// <summary>
    /// Gets or sets the rounding mode applied when a parameter supplies a scale.
    /// </summary>
    public MidpointRounding DecimalRoundingStrategy { get; init; } = MidpointRounding.AwayFromZero;
}
