using DataAccessLayer.Execution;
using FluentValidation;
using Shared.Configuration;

namespace DataAccessLayer.Validation;

/// <summary>
/// Validates parameter metadata before reaching the database helper.
/// </summary>
internal sealed class DbParameterDefinitionValidator : AbstractValidator<DbParameterDefinition>
{
    private readonly InputNormalizationOptions _options;

    public DbParameterDefinitionValidator(InputNormalizationOptions options)
    {
        _options = options ?? new InputNormalizationOptions();

        RuleFor(definition => definition.Name)
            .NotEmpty()
            .WithMessage("Parameter name is required.");

        RuleFor(definition => definition.Values)
            .NotNull()
            .When(definition => definition.TreatAsList)
            .WithMessage("Parameters marked as TreatAsList must include a Values collection.");

        RuleFor(definition => definition)
            .Custom(ValidateValues);
    }

    private void ValidateValues(DbParameterDefinition definition, ValidationContext<DbParameterDefinition> context)
    {
        ValidateSingle(definition, definition.Value, context);

        if (definition.TreatAsList && definition.Values is { } list)
        {
            foreach (var value in list)
            {
                ValidateSingle(definition, value, context);
            }
        }
    }

    private void ValidateSingle(
        DbParameterDefinition definition,
        object? value,
        ValidationContext<DbParameterDefinition> context)
    {
        switch (value)
        {
            case DateTime dateTime:
                AddViolation(InputNormalizationInspector.ValidateDate(definition, dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime(), _options), context);
                break;
            case DateTimeOffset offset:
                AddViolation(InputNormalizationInspector.ValidateDate(definition, offset.ToUniversalTime().DateTime, _options), context);
                break;
            case DateOnly dateOnly:
                AddViolation(InputNormalizationInspector.ValidateDate(definition, dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), _options), context);
                break;
            case decimal decimalValue:
                AddViolation(InputNormalizationInspector.ValidateDecimal(definition, decimalValue, _options), context);
                break;
        }
    }

    private static void AddViolation(string? violation, ValidationContext<DbParameterDefinition> context)
    {
        if (violation is not null)
        {
            context.AddFailure(violation);
        }
    }
}
