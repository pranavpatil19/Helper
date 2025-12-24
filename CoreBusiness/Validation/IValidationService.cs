using FluentValidation.Results;

namespace CoreBusiness.Validation;

/// <summary>
/// Central validation orchestrator that lets callers decide when to run validators.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates <paramref name="instance"/> using a registered validator, respecting global and per-call toggles.
    /// </summary>
    /// <param name="instance">Value to validate.</param>
    /// <param name="forceEnabled">Optional override; when set to false skips validation even if globally enabled.</param>
    /// <param name="ruleSets">Optional comma-separated rule set names.</param>
    /// <typeparam name="T">Validated type.</typeparam>
    /// <returns>ValidationResult; <see cref="ValidationResult.IsValid"/> indicates success.</returns>
    ValidationResult Validate<T>(T instance, bool? forceEnabled = null, string? ruleSets = null);
}
