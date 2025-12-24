using CoreBusiness.Models;
using FluentValidation;

namespace CoreBusiness.Validation;

/// <summary>
/// Common validation rules for any request that exposes title/notes semantics. Inherit and call <see cref="RegisterCommonRules"/> when shared behavior is needed; override <see cref="ConfigureCommonRules"/> to customize.
/// </summary>
/// <typeparam name="T">Request type implementing <see cref="ITodoRequest"/>.</typeparam>
public abstract class CommonValidationRulesBase<T> : AbstractValidator<T>
    where T : ITodoRequest
{
    protected void RegisterCommonRules()
    {
        ConfigureCommonRules();
    }

    protected virtual void ConfigureCommonRules()
    {
        RuleFor(request => request.Title)
            .Cascade(CascadeMode.Stop)
            .Must(HasVisibleCharacters)
            .WithMessage("Title must include at least one non-whitespace character.")
            .MaximumLength(256)
            .Must(HasNoEdgeWhitespace)
            .WithMessage("Title cannot start or end with whitespace.");

        RuleFor(request => request.Notes)
            .Cascade(CascadeMode.Stop)
            .Must(value => value is null || HasVisibleCharacters(value))
            .WithMessage("Notes cannot be whitespace only.")
            .Must(value => value is null || HasNoEdgeWhitespace(value))
            .WithMessage("Notes cannot start or end with whitespace.")
            .MaximumLength(1024);
    }

    protected static bool HasVisibleCharacters(string? value) =>
        !string.IsNullOrWhiteSpace(value);

    protected static bool HasNoEdgeWhitespace(string? value) =>
        value is null || value.Length == value.Trim().Length;
}
