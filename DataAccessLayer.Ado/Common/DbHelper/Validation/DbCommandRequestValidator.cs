using DataAccessLayer.Execution;
using FluentValidation;

namespace DataAccessLayer.Validation;

/// <summary>
/// FluentValidation rules for <see cref="DbCommandRequest"/>.
/// </summary>
internal sealed class DbCommandRequestValidator : AbstractValidator<DbCommandRequest>
{
    public DbCommandRequestValidator(IValidator<DbParameterDefinition> parameterValidator)
    {
        RuleFor(request => request.CommandText)
            .NotEmpty()
            .WithMessage("CommandText is required.");

        RuleFor(request => request.CommandTimeoutSeconds)
            .GreaterThan(0)
            .When(request => request.CommandTimeoutSeconds.HasValue);

        RuleFor(request => request.TraceName)
            .MaximumLength(128)
            .When(request => !string.IsNullOrWhiteSpace(request.TraceName));

        RuleForEach(request => request.Parameters)
            .SetValidator(parameterValidator);
    }
}
