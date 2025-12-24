using System.Buffers;
using System.Collections.Generic;
using System.Data;
using System.IO;
using DataAccessLayer.Execution;

namespace DataAccessLayer.Providers.Oracle;

/// <summary>
/// Helper methods for Oracle-specific parameter scenarios (REF CURSOR, array bind).
/// </summary>
public static class OracleParameterHelper
{
    /// <summary>
    /// Creates a REF CURSOR output parameter definition.
    /// </summary>
    public static DbParameterDefinition RefCursor(string name)
    {
        return new DbParameterDefinition
        {
            Name = name,
            Direction = ParameterDirection.Output,
            DbType = DbType.Object,
            ProviderTypeName = "RefCursor",
            IsNullable = true
        };
    }

    /// <summary>
    /// Creates an array binding parameter definition for Oracle using <see cref="DbType.Object"/>.
    /// </summary>
    public static DbParameterDefinition Array<T>(string name, IEnumerable<T> values, DbType dbType, int? size = null)
    {
        return StructuredParameterBuilder.OracleArray(name, values, dbType, size);
    }
}
