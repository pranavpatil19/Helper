using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using DataAccessLayer.Execution;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Default implementation that binds <see cref="DbParameterDefinition"/> objects
/// to provider-specific <see cref="DbParameter"/> instances.
/// </summary>
public sealed class ParameterBinder : IParameterBinder
{
    private readonly IDbParameterPool _parameterPool;
    private readonly ParameterBindingOptions _bindingOptions;
    private readonly InputNormalizationOptions _normalizationOptions;

    public ParameterBinder(
        IDbParameterPool parameterPool,
        ParameterBindingOptions? options = null,
        InputNormalizationOptions? normalizationOptions = null)
    {
        _parameterPool = parameterPool ?? throw new ArgumentNullException(nameof(parameterPool));
        _bindingOptions = options ?? new ParameterBindingOptions();
        _normalizationOptions = normalizationOptions ?? new InputNormalizationOptions();
    }

    public void BindParameters(DbCommand command, IReadOnlyList<DbParameterDefinition> definitions, DatabaseProvider provider)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (definitions is null || definitions.Count == 0)
        {
            return;
        }

        foreach (var definition in definitions)
        {
            var parameter = _parameterPool.Rent(command);
            PopulateParameter(parameter, definition, provider);
            command.Parameters.Add(parameter);
        }
    }

    private void PopulateParameter(
        DbParameter parameter,
        DbParameterDefinition definition,
        DatabaseProvider provider)
    {
        parameter.ParameterName = NormalizeParameterName(definition.Name, provider);
        parameter.Direction = definition.Direction;
        parameter.IsNullable = definition.IsNullable;

        if (definition.DbType is { } dbType)
        {
            parameter.DbType = dbType;
        }

        if (definition.Size is { } size)
        {
            parameter.Size = size;
        }

        if (definition.Precision is { } precision)
        {
            parameter.Precision = precision;
        }

        if (definition.Scale is { } scale)
        {
            parameter.Scale = scale;
        }

        var resolved = ResolveValue(definition);
        var coerced = ApplyProviderConversions(resolved, provider);
        parameter.Value = coerced;
        AdjustDbTypeAfterConversion(parameter, coerced, definition, provider);

        if (!string.IsNullOrWhiteSpace(definition.ProviderTypeName))
        {
            ValidateProviderTypeName(definition.ProviderTypeName);
            ApplyProviderTypeName(parameter, definition.ProviderTypeName);
        }
    }

    private object ResolveValue(DbParameterDefinition definition)
    {
        var raw = definition.TreatAsList && definition.Values is not null
            ? definition.Values
            : definition.Value;

        if (definition.ValueConverter is not null)
        {
            raw = definition.ValueConverter(raw);
        }

        raw = ApplyDefault(definition, raw);

        if (_bindingOptions.TrimStrings && raw is string stringValue)
        {
            var trimmed = stringValue.Trim();
            if (trimmed.Length == 0 && definition.IsNullable)
            {
                raw = null;
            }
            else
            {
                raw = trimmed;
            }
        }

        raw = _bindingOptions.ConvertEnumsToUnderlyingType
            ? ApplyEnumConversion(raw)
            : raw;

        raw = ValueNormalizer.Normalize(definition, raw, _normalizationOptions);

        return raw ?? DBNull.Value;
    }

    private static object? ApplyDefault(DbParameterDefinition definition, object? value)
    {
        if (value is null)
        {
            return definition.DefaultValue;
        }

        if (definition.DefaultValue is not null && IsDefaultStructValue(value))
        {
            return definition.DefaultValue;
        }

        return value;
    }

    private static bool IsDefaultStructValue(object value)
    {
        var type = value.GetType();
        if (!type.IsValueType)
        {
            return false;
        }

        try
        {
            var defaultValue = Activator.CreateInstance(type);
            return value.Equals(defaultValue);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeParameterName(string name, DatabaseProvider provider)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Parameter name cannot be empty.", nameof(name));
        }

        if (name[0] is '@' or ':' or '?')
        {
            return name;
        }

        return provider == DatabaseProvider.Oracle
            ? $":{name}"
            : $"@{name}";
    }

    private static void ApplyProviderTypeName(DbParameter parameter, string providerTypeName)
    {
        var property = parameter.GetType().GetProperty("DataTypeName");
        property?.SetValue(parameter, providerTypeName);
    }

    private static object? ApplyEnumConversion(object? value)
    {
        if (value is Enum enumValue)
        {
            return Convert.ChangeType(enumValue, Enum.GetUnderlyingType(enumValue.GetType()));
        }

        if (value is object?[] array)
        {
            var clone = new object?[array.Length];
            for (var i = 0; i < array.Length; i++)
            {
                clone[i] = ApplyEnumConversion(array[i]);
            }

            return clone;
        }

        return value;
    }

    private static object? ApplyProviderConversions(object? value, DatabaseProvider provider)
    {
        if (value is null)
        {
            return null;
        }


        if (value is object?[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = ApplyProviderConversions(array[i], provider);
            }

            return array;
        }

        if (value is DateTimeOffset dto)
        {
            if (provider == DatabaseProvider.PostgreSql || provider == DatabaseProvider.Oracle)
            {
                return dto.UtcDateTime;
            }

            return dto;
        }

        if (value is Guid guid && provider == DatabaseProvider.Oracle)
        {
            return guid.ToString("D");
        }

        return value;
    }

    private static void AdjustDbTypeAfterConversion(
        DbParameter parameter,
        object? value,
        DbParameterDefinition definition,
        DatabaseProvider provider)
    {
        if (value is null)
        {
            return;
        }

        if (value is DateTime &&
            (provider == DatabaseProvider.PostgreSql || provider == DatabaseProvider.Oracle) &&
            definition.DbType == DbType.DateTimeOffset)
        {
            parameter.DbType = DbType.DateTime;
        }

        if (value is string &&
            provider == DatabaseProvider.Oracle &&
            definition.DbType == DbType.Guid)
        {
            parameter.DbType = DbType.String;
        }

        if (value is string &&
            provider == DatabaseProvider.Oracle &&
            definition.DbType is null)
        {
            parameter.DbType = DbType.String;
        }
    }

    private void ValidateProviderTypeName(string providerTypeName)
    {
        if (_bindingOptions.AllowUnsafeProviderTypeNames)
        {
            return;
        }

        if (!IsSafeProviderTypeName(providerTypeName))
        {
            throw new ArgumentException(
                $"Provider type name '{providerTypeName}' contains disallowed characters. " +
                "Set ParameterBinding.AllowUnsafeProviderTypeNames = true if this is intentional.",
                nameof(providerTypeName));
        }
    }

    private static bool IsSafeProviderTypeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        if (value.Contains("--", StringComparison.Ordinal) ||
            value.Contains("/*", StringComparison.Ordinal) ||
            value.Contains("*/", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch) || ch == ';' || ch == '\'')
            {
                return false;
            }
        }

        return true;
    }
}
