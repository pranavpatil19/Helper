using CoreBusiness.Models;
using CoreBusiness.Validation;
using FluentValidation;

namespace CoreBusiness.Tests;

public sealed class CommonValidationRulesBaseTests
{
    [Fact]
    public void DefaultValidator_UsesCommonRules()
    {
        var validator = new DefaultTodoValidator();

        var result = validator.Validate(new TodoCreateRequest(string.Empty, new string('x', 2000)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(TodoCreateRequest.Title));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(TodoCreateRequest.Notes));
    }

    [Fact]
    public void DefaultValidator_FlagsWhitespaceOnlyTitle()
    {
        var validator = new DefaultTodoValidator();

        var result = validator.Validate(new TodoCreateRequest("   ", null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("non-whitespace", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultValidator_FlagsEdgeWhitespace()
    {
        var validator = new DefaultTodoValidator();

        var result = validator.Validate(new TodoCreateRequest(" padded ", " note "));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("cannot start or end", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DerivedValidator_CanOverrideRules()
    {
        var validator = new CustomTodoValidator();

        var result = validator.Validate(new TodoCreateRequest(new string('x', 500), null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("128", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class DefaultTodoValidator : CommonValidationRulesBase<TodoCreateRequest>
    {
        public DefaultTodoValidator()
        {
            RegisterCommonRules();
        }
    }

    private sealed class CustomTodoValidator : CommonValidationRulesBase<TodoCreateRequest>
    {
        public CustomTodoValidator()
        {
            RegisterCommonRules();
        }

        protected override void ConfigureCommonRules()
        {
            RuleFor(request => request.Title)
                .NotEmpty()
                .MaximumLength(128)
                .WithMessage("Title cannot exceed 128 characters for custom flows.");
        }
    }
}
