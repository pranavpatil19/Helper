using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using DataAccessLayer.Execution;

namespace DataAccessLayer.Providers.Postgres;

/// <summary>
/// Convenience methods for building PostgreSQL-specific parameters (JSONB, arrays, etc.).
/// </summary>
public static class PostgresParameterHelper
{
    /// <summary>
    /// Creates a JSONB parameter definition using <see cref="JsonSerializer"/>.
    /// </summary>
    public static DbParameterDefinition Jsonb<T>(string name, T value, JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var json = JsonSerializer.Serialize(value, options ?? DefaultSerializerOptions);
        return new DbParameterDefinition
        {
            Name = name,
            Value = json,
            DbType = DbType.String,
            ProviderTypeName = "jsonb",
            IsNullable = value is null
        };
    }

    /// <summary>
    /// Creates an array parameter definition for PostgreSQL using the provided CLR array.
    /// </summary>
    public static DbParameterDefinition Array<T>(string name, IEnumerable<T> values, string? providerTypeName = null)
    {
        var definition = StructuredParameterBuilder.PostgresArray(name, values);
        return new DbParameterDefinition
        {
            Name = definition.Name,
            Value = definition.Value,
            DbType = definition.DbType,
            Direction = definition.Direction,
            Size = definition.Size,
            Precision = definition.Precision,
            Scale = definition.Scale,
            IsNullable = definition.IsNullable,
            DefaultValue = definition.DefaultValue,
            ProviderTypeName = providerTypeName ?? definition.ProviderTypeName,
            TreatAsList = definition.TreatAsList,
            Values = definition.Values,
            ValueConverter = definition.ValueConverter
        };
    }

    private static readonly JsonSerializerOptions DefaultSerializerOptions = new(JsonSerializerDefaults.Web);
}
