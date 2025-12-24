using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace DataAccessLayer.Execution;

/// <summary>
/// Utility helpers that convert strongly-typed objects into <see cref="DbParameterDefinition"/> collections,
/// ensuring every parameter can opt into an explicit <see cref="DbType"/> when required by the caller.
/// </summary>
public static class DbParameterCollectionBuilder
{
    /// <summary>
    /// Projects a read-only list of parameters from an anonymous object's public properties.
    /// </summary>
    /// <param name="values">Anonymous object whose readable properties mirror the command's parameter names.</param>
    /// <param name="direction">Direction that will be applied to every generated parameter definition.</param>
    /// <returns>An ordered list of <see cref="DbParameterDefinition"/> instances or an empty array when <paramref name="values"/> is <c>null</c>.</returns>
    /// <remarks>
    /// This helper is convenient when a DTO already matches the expected parameter names. Because the API does not accept
    /// an explicit <see cref="DbType"/>, convert the returned instances with <see cref="Input(string, object?, DbType?, bool)"/>
    /// (or set <see cref="DbParameterDefinition.DbType"/> manually) whenever you need deterministic provider typing.
    /// </remarks>
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

    /// <summary>
    /// Creates parameter definitions from a dictionary that already maps logical names to values.
    /// </summary>
    /// <param name="values">Dictionary where the key is the logical parameter name and the value is the payload.</param>
    /// <param name="direction">Direction that will be applied to all generated definitions.</param>
    /// <returns>A list of <see cref="DbParameterDefinition"/> entries in enumeration order.</returns>
    /// <remarks>
    /// Use this overload when the parameter list is composed dynamically. Assign explicit <see cref="DbType"/> values
    /// by projecting the returned items into <see cref="Input(string, object?, DbType?, bool)"/> or by cloning
    /// <see cref="DbParameterDefinition"/> instances with the desired type.
    /// </remarks>
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

    /// <summary>
    /// Creates a strongly-typed input parameter definition.
    /// </summary>
    /// <param name="name">Logical parameter name without provider prefixes (e.g., no '@').</param>
    /// <param name="value">Value that will be assigned to the provider parameter.</param>
    /// <param name="dbType">Explicit <see cref="DbType"/>; specify to avoid provider inference.</param>
    /// <param name="isNullable">Marks whether the provider parameter should accept <c>null</c>.</param>
    /// <returns>A configured <see cref="DbParameterDefinition"/> representing an input parameter.</returns>
    public static DbParameterDefinition Input(
        string name,
        object? value,
        DbType? dbType = null,
        bool isNullable = false)
    {
        return Create(name, value, ParameterDirection.Input, dbType, isNullable);
    }

    /// <summary>
    /// Creates a parameter definition that represents a list of values (e.g., table-valued parameter or IN clause array).
    /// </summary>
    /// <typeparam name="T">Element type contained in <paramref name="values"/>.</typeparam>
    /// <param name="name">Logical name for the list parameter.</param>
    /// <param name="values">Sequence of values that will be materialized into the parameter definition.</param>
    /// <param name="dbType">Explicit <see cref="DbType"/> of the underlying provider parameter.</param>
    /// <param name="isNullable">Value indicating whether the provider parameter should accept <c>null</c> entries.</param>
    /// <returns>A <see cref="DbParameterDefinition"/> flagged with <see cref="DbParameterDefinition.TreatAsList"/>.</returns>
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

    /// <summary>
    /// Creates an output parameter definition (commonly used by stored procedures).
    /// </summary>
    /// <param name="name">Logical parameter name.</param>
    /// <param name="dbType">Explicit <see cref="DbType"/> expected from the provider.</param>
    /// <param name="size">Optional size for string/binary outputs.</param>
    /// <returns>A configured <see cref="DbParameterDefinition"/> for output scenarios.</returns>
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

    /// <summary>
    /// Creates a definition for a parameter that is both input and output.
    /// </summary>
    /// <param name="name">Logical parameter name.</param>
    /// <param name="value">Initial value supplied to the provider.</param>
    /// <param name="dbType">Explicit <see cref="DbType"/> that matches the provider parameter.</param>
    /// <param name="size">Optional size for string/binary parameters.</param>
    /// <returns>A <see cref="DbParameterDefinition"/> configured for <see cref="ParameterDirection.InputOutput"/>.</returns>
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

    /// <summary>
    /// Creates a return value parameter definition for stored procedures that emit scalar results through RETURN.
    /// </summary>
    /// <param name="name">Logical return parameter name; defaults to <c>"ReturnValue"</c>.</param>
    /// <param name="dbType">Explicit <see cref="DbType"/> expected from the provider.</param>
    /// <returns>A <see cref="DbParameterDefinition"/> configured for <see cref="ParameterDirection.ReturnValue"/>.</returns>
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
