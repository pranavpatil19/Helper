using FluentValidation;

namespace CoreBusiness.Validation;

/// <summary>
/// Helper methods for <see cref="IValidationService"/>.
/// </summary>
public static class ValidationServiceExtensions
{
    /// <summary>
    /// Runs validation and throws <see cref="ValidationException"/> when invalid.
    /// </summary>
    public static void ValidateAndThrow<T>(this IValidationService service, T instance, bool? forceEnabled = null, string? ruleSets = null)
    {
        var result = service.Validate(instance, forceEnabled, ruleSets);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }
}
