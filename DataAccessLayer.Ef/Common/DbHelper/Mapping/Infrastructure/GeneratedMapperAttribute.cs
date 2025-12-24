using System;

namespace DataAccessLayer.Mapping;

/// <summary>
/// Marks an entity for which a source-generated <see cref="IDataMapper{T}"/> should be emitted.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class GeneratedMapperAttribute : Attribute
{
    public GeneratedMapperAttribute(string mapperName)
    {
        MapperName = mapperName;
    }

    public string MapperName { get; }
}
