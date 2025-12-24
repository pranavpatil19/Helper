using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreBusiness.Validation;

/// <summary>
/// Default validator orchestrator. Honors global options and lets callers override per request.
/// </summary>
public sealed class ValidationService : IValidationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ValidationOptions _options;

    public ValidationService(IServiceProvider serviceProvider, IOptions<ValidationOptions> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public ValidationResult Validate<T>(T instance, bool? forceEnabled = null, string? ruleSets = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var enabled = forceEnabled ?? _options.Enabled;
        if (!enabled)
        {
            return new ValidationResult();
        }

        var validator = _serviceProvider.GetService<IValidator<T>>();
        if (validator is null)
        {
            return new ValidationResult();
        }

        var effectiveRuleSets = string.IsNullOrWhiteSpace(ruleSets)
            ? _options.DefaultRuleSets
            : ruleSets;

        if (string.IsNullOrWhiteSpace(effectiveRuleSets))
        {
            return validator.Validate(instance);
        }

        var tokens = effectiveRuleSets
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return validator.Validate(instance);
        }

        var selector = new RulesetValidatorSelector(tokens);
        var context = new ValidationContext<T>(instance, new PropertyChain(), selector);
        return validator.Validate(context);
    }
}
