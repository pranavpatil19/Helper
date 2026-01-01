using System;
using DataAccessLayer.Execution;
using DataAccessLayer.Validation;
using FluentValidation;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests.Validation;

public sealed class DbCommandRequestValidatorTests
{
    private static IValidator<DbCommandRequest> CreateValidator(InputNormalizationOptions? options = null)
    {
        options ??= new InputNormalizationOptions();
        var parameterValidator = new DbParameterDefinitionValidator(options);
        return new DbCommandRequestValidator(parameterValidator);
    }

    [Fact]
    public void Validate_Fails_WhenCommandTextMissing()
    {
        var validator = CreateValidator();
        var result = validator.Validate(new DbCommandRequest());
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenDecimalExceedsPrecision()
    {
        var validator = CreateValidator();
        var request = new DbCommandRequest
        {
            CommandText = "SELECT 1",
            Parameters =
            [
                new DbParameterDefinition
                {
                    Name = "amount",
                    Precision = 5,
                    Scale = 2,
                    Value = 1234.56m
                }
            ]
        };

        var result = validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("amount", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_Fails_WhenDateOutsideRange()
    {
        var options = new InputNormalizationOptions
        {
            MinDateUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            MaxDateUtc = new DateTime(2050, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var validator = CreateValidator(options);
        var request = new DbCommandRequest
        {
            CommandText = "SELECT 1",
            Parameters =
            [
                new DbParameterDefinition
                {
                    Name = "cutoff",
                    Value = new DateTime(1999, 12, 31, 0, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        var result = validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("cutoff", StringComparison.OrdinalIgnoreCase));
    }
}
