using System.Collections.Generic;
using System.Data.Common;
using Shared.Configuration;

namespace DataAccessLayer.Execution;

/// <summary>
/// Binds <see cref="DbParameterDefinition"/> instances to provider-specific <see cref="DbParameter"/> objects,
/// honoring the explicit <see cref="System.Data.DbType"/> and provider metadata configured on each definition.
/// </summary>
/// <remarks>
/// Implementations typically convert normalized definitions created by <see cref="DbParameterCollectionBuilder"/>
/// or <see cref="StructuredParameterBuilder"/> into the concrete parameter classes expected by SQL Server,
/// PostgreSQL, or Oracle drivers. Every binding operation should carry the <c>DbType</c> forward so provider
/// inference never strips important metadata such as precision, scale, or array typing.
/// </remarks>
public interface IParameterBinder
{
    /// <summary>
    /// Binds the provided parameter definitions to the <paramref name="command"/>, creating provider parameters that
    /// mirror each definition's <see cref="DbParameterDefinition.DbType"/>, size, precision, and direction metadata.
    /// </summary>
    /// <param name="command">Command that will receive the created parameters.</param>
    /// <param name="definitions">Logical parameter definitions.</param>
    /// <param name="provider">Provider the command targets.</param>
    void BindParameters(DbCommand command, IReadOnlyList<DbParameterDefinition> definitions, DatabaseProvider provider);
}
