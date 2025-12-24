using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Shared.Configuration;

namespace DataAccessLayer.Execution;

/// <summary>
/// Encapsulates everything required to execute a database command in a provider-agnostic manner.
/// </summary>
public sealed class DbCommandRequest
{
    /// <summary>
    /// Gets or sets the SQL text or stored procedure name to execute.
    /// </summary>
    public string CommandText { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the command type (text vs stored procedure).
    /// </summary>
    public CommandType CommandType { get; init; } = CommandType.Text;

    /// <summary>
    /// Gets or sets the collection of parameters to bind before execution.
    /// </summary>
    public IReadOnlyList<DbParameterDefinition> Parameters { get; init; } = Array.Empty<DbParameterDefinition>();

    /// <summary>
    /// Gets or sets the timeout in seconds. When null the provider default is used.
    /// </summary>
    public int? CommandTimeoutSeconds { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="DbCommand.Prepare"/> should be invoked.
    /// </summary>
    public bool PrepareCommand { get; init; }

    /// <summary>
    /// Gets or sets an optional connection to reuse. When null the helper opens a new connection.
    /// </summary>
    public DbConnection? Connection { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the helper should close the provided <see cref="Connection"/>.
    /// </summary>
    public bool CloseConnection { get; init; }

    /// <summary>
    /// Gets or sets the transaction to enlist the command within.
    /// </summary>
    public DbTransaction? Transaction { get; init; }

    /// <summary>
    /// Gets or sets provider configuration overrides. When null the global <see cref="DatabaseOptions"/> are used.
    /// </summary>
    public DatabaseOptions? OverrideOptions { get; init; }

    /// <summary>
    /// Gets or sets additional flags that influence how readers behave (streaming, sequential access, etc.).
    /// </summary>
    public CommandBehavior CommandBehavior { get; init; } = CommandBehavior.Default;

    /// <summary>
    /// Gets or sets an optional label used in telemetry/logging pipelines.
    /// </summary>
    public string? TraceName { get; init; }
}
