# Validation Pipeline

Validation is always enabled for both ADO and EF entry points. Calling `AddDataAccessLayer` automatically registers `DbParameterDefinitionValidator` (guards parameter shape/precision) and `DbCommandRequestValidator` (ensures commands contain normalized text, parameters, and trace metadata). There is no feature flag or pass-through validator—requests must satisfy the shared rules before the DAL talks to the provider.

Need project-specific rules? Decorate or replace the built-in validators through DI:

```csharp
builder.Services.AddTransient<IValidator<DbCommandRequest>>(sp =>
    new CustomCommandRequestValidator(
        sp.GetRequiredService<IValidator<DbCommandRequest>>(),
        sp.GetRequiredService<ILogger<CustomCommandRequestValidator>>()));
```

At the business layer, inherit from `CoreBusiness.Validation.CommonValidationRulesBase<T>` when multiple request types share the same FluentValidation rules. Derived validators call `RegisterCommonRules()` to reuse the base checks and override `ConfigureCommonRules()` when a specific endpoint needs stricter behavior.
