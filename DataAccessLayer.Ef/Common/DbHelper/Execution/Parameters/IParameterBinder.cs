using System.Collections.Generic;
using System.Data.Common;
using Shared.Configuration;

namespace DataAccessLayer.Execution;

/// <summary>
/// Binds <see cref="DbParameterDefinition"/> instances to provider-specific <see cref="DbParameter"/> objects.
/// </summary>
public interface IParameterBinder
{
    /// <summary>
    /// Binds the provided parameter definitions to the <paramref name="command"/>.
    /// </summary>
    /// <param name="command">Command that will receive the created parameters.</param>
    /// <param name="definitions">Logical parameter definitions.</param>
    /// <param name="provider">Provider the command targets.</param>
    void BindParameters(DbCommand command, IReadOnlyList<DbParameterDefinition> definitions, DatabaseProvider provider);
}
