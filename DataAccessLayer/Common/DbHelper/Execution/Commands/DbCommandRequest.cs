using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Shared.Configuration;

namespace DataAccessLayer.Execution;

/// <summary>
/// Encapsulates everything the top-level <see cref="IDatabaseHelper"/> entry points need to execute a command.
/// </summary>
/// <remarks>
/// Requests created here are ultimately fed to <see cref="IDatabaseHelper.ExecuteAsync"/>, <see cref="IDatabaseHelper.Execute"/>,
/// <see cref="IDatabaseHelper.QueryAsync{T}(DbCommandRequest, System.Func{System.Data.Common.DbDataReader, T}, System.Threading.CancellationToken)"/>,
/// <see cref="IDatabaseHelper.Query{T}(DbCommandRequest, System.Func{System.Data.Common.DbDataReader, T})"/>, and the related streaming/load helpers.
/// Always populate <see cref="Parameters"/> with <see cref="DbParameterDefinition"/> instances (for example via
/// <see cref="DataAccessLayer.Execution.Builders.DbParameter"/>) so the subsequent provider binding remains deterministic.
/// </remarks>
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

    /// <summary>
    /// <summary>
    /// Gets or sets a value indicating whether FluentValidation validators should run (true by default).
    /// Set to <c>false</c> only when inputs are already validated explicitly.
    /// </summary>
    public bool Validate { get; init; } = true;
}
