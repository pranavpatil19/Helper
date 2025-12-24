using System;
using System.Collections.Generic;
using System.Linq;
using DataAccessLayer.Execution;

namespace DataAccessLayer.EF;

/// <summary>
/// Describes a compiled EF Core query along with the parameter profile it should honor.
/// Use <see cref="Create(string, DbParameterDefinition[])"/> to ensure the profile
/// stays aligned with <see cref="DbParameterDefinition"/> metadata used elsewhere in the DAL.
/// </summary>
public sealed class CompiledQueryDescriptor
{
    private CompiledQueryDescriptor(string key, IReadOnlyList<DbParameterDefinition> parameterProfile)
    {
        Key = key;
        ParameterProfile = parameterProfile;
    }

    /// <summary>
    /// Gets the unique key that identifies the compiled query.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the parameter profile (may be empty for parameter-less queries).
    /// </summary>
    public IReadOnlyList<DbParameterDefinition> ParameterProfile { get; }

    /// <summary>
    /// Creates a descriptor for the supplied key and parameter definitions.
    /// The definitions are cloned so that runtime values aren't captured.
    /// </summary>
    public static CompiledQueryDescriptor Create(string key, params DbParameterDefinition[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var profile = parameters is { Length: > 0 }
            ? parameters.Select(CloneDefinition).ToArray()
            : Array.Empty<DbParameterDefinition>();
        return new CompiledQueryDescriptor(key, profile);
    }

    private static DbParameterDefinition CloneDefinition(DbParameterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new DbParameterDefinition
        {
            Name = definition.Name,
            DbType = definition.DbType,
            Direction = definition.Direction,
            Size = definition.Size,
            Precision = definition.Precision,
            Scale = definition.Scale,
            IsNullable = definition.IsNullable,
            DefaultValue = definition.DefaultValue,
            ProviderTypeName = definition.ProviderTypeName,
            TreatAsList = definition.TreatAsList,
            Values = definition.Values,
            ValueConverter = definition.ValueConverter
        };
    }
}
