using System.Diagnostics.CodeAnalysis;
using CoreBusiness.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreBusiness.Tests;

public sealed class ValidationServiceTests
{
    [Fact]
    public void Validate_Runs_WhenGloballyEnabled()
    {
        using var provider = BuildProvider(enabled: true);
        var service = provider.GetRequiredService<IValidationService>();

        var result = service.Validate(new DemoRequest(string.Empty));

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Validate_Skips_WhenGloballyDisabled()
    {
        using var provider = BuildProvider(enabled: false);
        var service = provider.GetRequiredService<IValidationService>();

        var result = service.Validate(new DemoRequest(string.Empty));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RespectsForceOverride()
    {
        using var provider = BuildProvider(enabled: true);
        var service = provider.GetRequiredService<IValidationService>();

        var result = service.Validate(new DemoRequest(string.Empty), forceEnabled: false);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_CanTargetRuleSets()
    {
        using var provider = BuildProvider(enabled: true);
        var service = provider.GetRequiredService<IValidationService>();

        var result = service.Validate(new DemoRequest("abc"), ruleSets: "Strict");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("least 5", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateAndThrow_Throws_WhenInvalid()
    {
        using var provider = BuildProvider(enabled: true);
        var service = provider.GetRequiredService<IValidationService>();

        var ex = Assert.Throws<ValidationException>(() => service.ValidateAndThrow(new DemoRequest(string.Empty)));
        Assert.Single(ex.Errors);
    }

    [Fact]
    public void Validate_UsesDefaultRuleSets_WhenConfigured()
    {
        using var provider = BuildProvider(enabled: true, defaultRuleSets: "Strict");
        var service = provider.GetRequiredService<IValidationService>();

        var result = service.Validate(new DemoRequest("abc"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("least 5", StringComparison.OrdinalIgnoreCase));
    }

    private static ServiceProvider BuildProvider(bool enabled, string? defaultRuleSets = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<IValidator<DemoRequest>, DemoRequestValidator>();
        services.AddScoped<IValidationService, ValidationService>();
        services.AddSingleton<IOptions<ValidationOptions>>(new OptionsWrapper<ValidationOptions>(new ValidationOptions
        {
            Enabled = enabled,
            DefaultRuleSets = defaultRuleSets
        }));

        return services.BuildServiceProvider();
    }

    private sealed record DemoRequest(string Value);

    [SuppressMessage("SonarAnalyzer.CSharp", "S1144", Justification = "Instantiated via DI during tests.")]
    private sealed class DemoRequestValidator : AbstractValidator<DemoRequest>
    {
        public DemoRequestValidator()
        {
            RuleFor(x => x.Value).NotEmpty();

            RuleSet("Strict", () =>
            {
                RuleFor(x => x.Value).MinimumLength(5).MaximumLength(10);
            });
        }
    }
}
