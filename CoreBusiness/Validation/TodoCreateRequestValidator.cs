using CoreBusiness.Models;
using FluentValidation;

namespace CoreBusiness.Validation;

/// <summary>
/// Validates incoming task creation requests before the DAL is invoked using the shared to-do rules.
/// </summary>
public sealed class TodoCreateRequestValidator : CommonValidationRulesBase<TodoCreateRequest>
{
    public TodoCreateRequestValidator()
    {
        RegisterCommonRules();
    }
}
