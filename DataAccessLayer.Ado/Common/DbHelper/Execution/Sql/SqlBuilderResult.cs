using System.Collections.Generic;
using System.Data;
using Shared.Configuration;

namespace DataAccessLayer.Execution;

/// <summary>
/// Represents the SQL text and parameters produced by <see cref="SqlBuilder"/>.
/// </summary>
public sealed class SqlBuilderResult
{
    public SqlBuilderResult(string commandText, DatabaseProvider provider, IReadOnlyList<DbParameterDefinition> parameters)
    {
        CommandText = commandText;
        Provider = provider;
        Parameters = parameters;
    }

    /// <summary>
    /// Gets the final SQL string with provider-specific parameter tokens.
    /// </summary>
    public string CommandText { get; }

    /// <summary>
    /// Gets the provider used when rendering the SQL.
    /// </summary>
    public DatabaseProvider Provider { get; }

    /// <summary>
    /// Gets the ordered parameter definitions required by the command.
    /// </summary>
    public IReadOnlyList<DbParameterDefinition> Parameters { get; }

    /// <summary>
    /// Converts the result into a <see cref="DbCommandRequest"/>.
    /// </summary>
    public DbCommandRequest ToRequest(CommandType commandType = CommandType.Text) =>
        new()
        {
            CommandText = CommandText,
            CommandType = commandType,
            Parameters = Parameters
        };
}
