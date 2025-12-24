namespace CoreBusiness.Validation;

/// <summary>
/// Controls whether validators run. Toggle <see cref="Enabled"/> false to bypass all validation globally.
/// </summary>
public sealed class ValidationOptions
{
    public const string SectionName = "Validation";

    public bool Enabled { get; set; } = true;
    /// <summary>
    /// Optional comma-separated default rule sets applied when callers do not pass <c>ruleSets</c>.
    /// </summary>
    public string? DefaultRuleSets { get; set; }
}
