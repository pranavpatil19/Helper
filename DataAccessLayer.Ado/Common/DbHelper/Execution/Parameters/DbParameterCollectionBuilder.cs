using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace DataAccessLayer.Execution;

/// <summary>
/// Utility helpers that convert strongly-typed objects into <see cref="DbParameterDefinition"/> collections.
/// </summary>
public static class DbParameterCollectionBuilder
{
    public static IReadOnlyList<DbParameterDefinition> FromAnonymous(
        object? values,
        ParameterDirection direction = ParameterDirection.Input)
    {
        if (values is null)
        {
            return Array.Empty<DbParameterDefinition>();
        }

        var properties = values
            .GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var result = new List<DbParameterDefinition>(properties.Length);

        foreach (var property in properties)
        {
            if (!property.CanRead)
            {
                continue;
            }

            var value = property.GetValue(values);
            result.Add(Create(property.Name, value, direction));
        }

        return result;
    }

    public static IReadOnlyList<DbParameterDefinition> FromDictionary(
        IReadOnlyDictionary<string, object?>? values,
        ParameterDirection direction = ParameterDirection.Input)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<DbParameterDefinition>();
        }

        var result = new List<DbParameterDefinition>(values.Count);
        foreach (var (key, value) in values)
        {
            result.Add(Create(key, value, direction));
        }

        return result;
    }

    public static DbParameterDefinition Input(
        string name,
        object? value,
        DbType? dbType = null,
        bool isNullable = false)
    {
        return Create(name, value, ParameterDirection.Input, dbType, isNullable);
    }

    public static DbParameterDefinition InputList<T>(
        string name,
        IEnumerable<T> values,
        DbType? dbType = null,
        bool isNullable = false)
    {
        var list = MaterializeList(values);
        return new DbParameterDefinition
        {
            Name = name,
            Direction = ParameterDirection.Input,
            DbType = dbType,
            IsNullable = isNullable,
            TreatAsList = true,
            Values = list
        };
    }

    public static DbParameterDefinition Output(
        string name,
        DbType? dbType = null,
        int? size = null)
    {
        return new DbParameterDefinition
        {
            Name = name,
            Direction = ParameterDirection.Output,
            DbType = dbType,
            Size = size,
            IsNullable = true
        };
    }

    public static DbParameterDefinition InputOutput(
        string name,
        object? value,
        DbType? dbType = null,
        int? size = null)
    {
        return new DbParameterDefinition
        {
            Name = name,
            Value = value,
            Direction = ParameterDirection.InputOutput,
            DbType = dbType,
            Size = size,
            IsNullable = true
        };
    }

    public static DbParameterDefinition ReturnValue(
        string name = "ReturnValue",
        DbType? dbType = null)
    {
        return new DbParameterDefinition
        {
            Name = name,
            Direction = ParameterDirection.ReturnValue,
            DbType = dbType,
            IsNullable = true
        };
    }

    private static DbParameterDefinition Create(
        string name,
        object? value,
        ParameterDirection direction,
        DbType? dbType = null,
        bool isNullable = false)
    {
        if (TryMaterializeList(value, out var list))
        {
            return new DbParameterDefinition
            {
                Name = name,
                Direction = direction,
                DbType = dbType,
                IsNullable = true,
                TreatAsList = true,
                Values = list
            };
        }

        return new DbParameterDefinition
        {
            Name = name,
            Direction = direction,
            DbType = dbType,
            Value = value,
            IsNullable = isNullable || value is null
        };
    }

    private static bool TryMaterializeList(object? value, out IReadOnlyList<object?>? list)
    {
        list = null;

        if (value is null || value is string || value is byte[] || value is char[])
        {
            return false;
        }

        if (value is IEnumerable enumerable)
        {
            list = MaterializeList(enumerable);
            return true;
        }

        return false;
    }

    private static IReadOnlyList<object?> MaterializeList(IEnumerable values)
    {
        var buffer = new List<object?>();
        foreach (var item in values)
        {
            buffer.Add(item);
        }

        return buffer;
    }
}
