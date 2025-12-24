using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Mapping;

/// <summary>
/// High-performance mapper that uses compiled delegates (expression-based IL) to set properties.
/// </summary>
/// <typeparam name="T">Destination type.</typeparam>
public sealed class IlEmitDataMapper<T> : IDataMapper<T>
    where T : class
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, Setter>> SetterCache = new();
    private static readonly Func<T> InstanceFactory = CreateFactory();
    private readonly IReadOnlyDictionary<string, Setter> _setters;

    public IlEmitDataMapper(
        bool ignoreCase = true,
        IReadOnlyDictionary<string, string>? propertyToColumnMap = null)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var baseSetters = SetterCache.GetOrAdd(typeof(T), type => BuildSetters(type, comparer));
        _setters = BuildColumnLookup(baseSetters, propertyToColumnMap, comparer);
    }

    public T Map(DbDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var instance = InstanceFactory();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            if (!_setters.TryGetValue(name, out var setter))
            {
                continue;
            }

            if (reader.IsDBNull(i))
            {
                continue;
            }

            var rawValue = reader.GetValue(i);
            var converted = setter.ConvertValue(rawValue);
            setter.Action(instance, converted);
        }

        return instance;
    }

    private static IReadOnlyDictionary<string, Setter> BuildSetters(Type type, StringComparer comparer)
    {
        var result = new Dictionary<string, Setter>(comparer);
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanWrite)
            {
                continue;
            }

            result[property.Name] = new Setter(property);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, Setter> BuildColumnLookup(
        IReadOnlyDictionary<string, Setter> baseSetters,
        IReadOnlyDictionary<string, string>? propertyToColumnMap,
        StringComparer comparer)
    {
        if (propertyToColumnMap is null || propertyToColumnMap.Count == 0)
        {
            return baseSetters;
        }

        var lookup = new Dictionary<string, Setter>(baseSetters.Count + propertyToColumnMap.Count, comparer);
        foreach (var pair in baseSetters)
        {
            lookup[pair.Key] = pair.Value;
        }

        foreach (var (propertyName, columnName) in propertyToColumnMap)
        {
            if (string.IsNullOrWhiteSpace(propertyName) || string.IsNullOrWhiteSpace(columnName))
            {
                continue;
            }

            if (baseSetters.TryGetValue(propertyName, out var setter))
            {
                lookup[columnName] = setter;
            }
        }

        return lookup;
    }

    private static Func<T> CreateFactory()
    {
        var constructor = typeof(T).GetConstructor(Type.EmptyTypes);
        if (constructor is null)
        {
            throw new RowMappingException($"Type '{typeof(T).FullName}' must expose a parameterless constructor.");
        }

        return Expression.Lambda<Func<T>>(Expression.New(constructor)).Compile();
    }

    private sealed class Setter
    {
        private readonly Func<object?, object?> _converter;
        public Action<T, object?> Action { get; }

        public Setter(PropertyInfo property)
        {
            Action = CreateSetter(property);
            _converter = CreateConverter(property.PropertyType);
        }

        public object? ConvertValue(object? value) => _converter(value);

        private static Action<T, object?> CreateSetter(PropertyInfo property)
        {
            var instanceParam = Expression.Parameter(typeof(T), "instance");
            var valueParam = Expression.Parameter(typeof(object), "value");

            var assign = Expression.Assign(
                Expression.Property(instanceParam, property),
                Expression.Convert(valueParam, property.PropertyType));

            return Expression.Lambda<Action<T, object?>>(assign, instanceParam, valueParam).Compile();
        }

        private static Func<object?, object?> CreateConverter(Type targetType)
        {
            var valueParam = Expression.Parameter(typeof(object), "value");
            var underlying = Nullable.GetUnderlyingType(targetType);
            var nonNullableTarget = underlying ?? targetType;

            Expression body;
            var valueIsNull = Expression.Equal(valueParam, Expression.Constant(null));

            Expression notNullExpression;
            if (targetType == typeof(object))
            {
                notNullExpression = valueParam;
            }
            else
            {
                var changeTypeCall = Expression.Call(
                    typeof(Convert),
                    nameof(Convert.ChangeType),
                    Type.EmptyTypes,
                    valueParam,
                    Expression.Constant(nonNullableTarget));

                var converted = Expression.Convert(changeTypeCall, nonNullableTarget);
                if (underlying is not null)
                {
                    converted = Expression.Convert(converted, targetType);
                }

                notNullExpression = converted;
            }

            var whenNull = Expression.Convert(Expression.Default(targetType), typeof(object));
            var whenNotNull = Expression.Convert(notNullExpression, typeof(object));

            body = Expression.Condition(valueIsNull, whenNull, whenNotNull);

            return Expression.Lambda<Func<object?, object?>>(body, valueParam).Compile();
        }
    }
}
