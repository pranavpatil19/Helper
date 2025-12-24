namespace DataAccessLayer.Mapping;

/// <summary>
/// Represents the mapping strategy used to materialize objects from a data reader.
/// </summary>
public enum MapperStrategy
{
    /// <summary>
    /// Reflection-based property mapper.
    /// </summary>
    Reflection,

    /// <summary>
    /// IL/emitted setter mapper for high-performance materialization.
    /// </summary>
    IlEmit,

    /// <summary>
    /// Maps each row to <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// </summary>
    Dictionary,

    /// <summary>
    /// Maps each row to a <see cref="System.Dynamic.ExpandoObject"/> (dynamic).
    /// </summary>
    Dynamic,

    /// <summary>
    /// Uses a source-generated mapper decorated with <see cref="GeneratedMapperAttribute"/>.
    /// </summary>
    SourceGenerator
}
