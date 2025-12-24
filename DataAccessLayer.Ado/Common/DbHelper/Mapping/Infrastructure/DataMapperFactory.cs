using System;
using System.Collections.Generic;
using System.Reflection;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Creates mapper instances based on the desired strategy.
/// </summary>
public static class DataMapperFactory
{
    /// <summary>
    /// Creates an <see cref="IDataMapper{T}"/> using the specified strategy.
    /// </summary>
    /// <typeparam name="T">Destination type.</typeparam>
    /// <param name="strategy">The mapping strategy to use.</param>
    /// <param name="ignoreCase">When true, comparisons ignore column/property casing.</param>
    /// <returns>An <see cref="IDataMapper{T}"/> implementation.</returns>
    /// <exception cref="RowMappingException">Thrown when the requested strategy is incompatible with <typeparamref name="T"/>.</exception>
    public static IDataMapper<T> CreateMapper<T>(
        MapperStrategy strategy = MapperStrategy.Reflection,
        bool ignoreCase = true,
        IReadOnlyDictionary<string, string>? propertyToColumnMap = null,
        IReadOnlyList<IMappingProfile>? profiles = null)
        where T : class
    {
        if (propertyToColumnMap is not null &&
            strategy is not MapperStrategy.Reflection and not MapperStrategy.IlEmit)
        {
            throw new RowMappingException("Column maps are supported for Reflection and IlEmit strategies only.");
        }

        return strategy switch
        {
            MapperStrategy.Reflection => new ReflectionDataMapper<T>(ignoreCase, propertyToColumnMap, profiles),
            MapperStrategy.IlEmit => new IlEmitDataMapper<T>(ignoreCase, propertyToColumnMap),
            MapperStrategy.Dictionary => CreateDictionaryMapper<T>(ignoreCase),
            MapperStrategy.Dynamic => CreateDynamicMapper<T>(ignoreCase),
            MapperStrategy.SourceGenerator => CreateGeneratedMapper<T>(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null)
        };
    }

    private static IDataMapper<T> CreateGeneratedMapper<T>() where T : class
    {
        var attribute = typeof(T).GetCustomAttribute<GeneratedMapperAttribute>();
        if (attribute is null)
        {
            throw new RowMappingException($"Type {typeof(T).Name} is not annotated with [GeneratedMapper].");
        }

        var mapperName = attribute.MapperName;
        var mapperType = typeof(T).Assembly.GetType(mapperName)
            ?? typeof(T).Assembly.GetType($"{typeof(T).Namespace}.{mapperName}")
            ?? Type.GetType(mapperName, throwOnError: false);

        if (mapperType is null)
        {
            throw new RowMappingException($"Source-generated mapper '{mapperName}' was not found.");
        }

        return (IDataMapper<T>)Activator.CreateInstance(mapperType)!;
    }

    private static IDataMapper<T> CreateDictionaryMapper<T>(bool ignoreCase)
    {
        if (typeof(T) != typeof(IReadOnlyDictionary<string, object?>))
        {
            throw new RowMappingException(
                $"MapperStrategy.Dictionary requires T to be IReadOnlyDictionary<string, object?>, but was {typeof(T).Name}.");
        }

        IDataMapper<IReadOnlyDictionary<string, object?>> mapper = new DictionaryDataMapper(ignoreCase);
        return (IDataMapper<T>)mapper;
    }

    private static IDataMapper<T> CreateDynamicMapper<T>(bool ignoreCase)
    {
        if (typeof(T) != typeof(object))
        {
            throw new RowMappingException(
                $"MapperStrategy.Dynamic requires T to be object (dynamic). Provided type: {typeof(T).Name}.");
        }

        IDataMapper<object> mapper = new DynamicDataMapper(ignoreCase);
        return (IDataMapper<T>)mapper;
    }
}
