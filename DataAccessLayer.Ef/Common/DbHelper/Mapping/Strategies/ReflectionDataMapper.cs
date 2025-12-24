using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Reflection-based mapper that binds reader columns to writable properties on <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Destination type.</typeparam>
public sealed class ReflectionDataMapper<T> : IDataMapper<T>
    where T : class
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyAccessor>> PropertyCache = new();
    private static readonly Func<T> InstanceFactory = CreateFactory();
    private readonly IReadOnlyDictionary<string, PropertyAccessor> _columnLookup;
    private readonly StringComparison _comparison;
    private readonly IMappingProfile[] _profiles;

    public ReflectionDataMapper(
        bool ignoreCase = true,
        IReadOnlyDictionary<string, string>? propertyToColumnMap = null,
        IReadOnlyList<IMappingProfile>? profiles = null)
    {
        _comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var baseMap = PropertyCache.GetOrAdd(typeof(T), BuildPropertyMap);
        _columnLookup = BuildColumnLookup(baseMap, propertyToColumnMap, ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        _profiles = profiles?.ToArray() ?? Array.Empty<IMappingProfile>();
    }

    public T Map(DbDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var instance = InstanceFactory();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            if (!_columnLookup.TryGetValue(columnName, out var accessor))
            {
                var match = _columnLookup.FirstOrDefault(pair => string.Equals(pair.Key, columnName, _comparison));
                if (!string.IsNullOrEmpty(match.Key))
                {
                    accessor = match.Value;
                }
            }

            if (accessor is null)
            {
                continue;
            }

            object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            if (value is not null &&
                _profiles.Length > 0 &&
                TryApplyProfiles(columnName, accessor.PropertyName, accessor.TargetType, value, out var converted))
            {
                value = converted;
            }

            accessor.SetValue(instance, value);
        }

        return instance;
    }

    private static IReadOnlyDictionary<string, PropertyAccessor> BuildPropertyMap(Type type)
    {
        var result = new Dictionary<string, PropertyAccessor>(StringComparer.OrdinalIgnoreCase);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            if (!property.CanWrite)
            {
                continue;
            }

            result[property.Name] = new PropertyAccessor(property.Name, property);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, PropertyAccessor> BuildColumnLookup(
        IReadOnlyDictionary<string, PropertyAccessor> baseMap,
        IReadOnlyDictionary<string, string>? propertyToColumnMap,
        StringComparer comparer)
    {
        if (propertyToColumnMap is null || propertyToColumnMap.Count == 0)
        {
            return baseMap;
        }

        var lookup = new Dictionary<string, PropertyAccessor>(baseMap.Count + propertyToColumnMap.Count, comparer);
        foreach (var pair in baseMap)
        {
            lookup[pair.Key] = pair.Value;
        }

        foreach (var (propertyName, columnName) in propertyToColumnMap)
        {
            if (string.IsNullOrWhiteSpace(propertyName) || string.IsNullOrWhiteSpace(columnName))
            {
                continue;
            }

            if (baseMap.TryGetValue(propertyName, out var accessor))
            {
                lookup[columnName] = accessor;
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

    private bool TryApplyProfiles(string columnName, string propertyName, Type targetType, object value, out object? converted)
    {
        foreach (var profile in _profiles)
        {
            if (profile.TryConvert(columnName, propertyName, targetType, value, out converted))
            {
                return true;
            }
        }

        converted = null;
        return false;
    }

    private sealed class PropertyAccessor
    {
        private readonly PropertyInfo _property;
        private readonly Type _targetType;

        public PropertyAccessor(string propertyName, PropertyInfo property)
        {
            PropertyName = propertyName;
            _property = property;
            _targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        }

        public string PropertyName { get; }
        public Type TargetType => _targetType;

        public void SetValue(object instance, object? value)
        {
            if (value is null)
            {
                _property.SetValue(instance, null);
                return;
            }

            var valueType = value.GetType();
            if (!_targetType.IsAssignableFrom(valueType))
            {
                value = Convert.ChangeType(value, _targetType);
            }

            _property.SetValue(instance, value);
        }
    }
}
