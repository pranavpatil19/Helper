using System;
using System.Data;

namespace DataAccessLayer.Execution;

/// <summary>
/// Convenience helpers for working with <see cref="DbCommandRequest"/> instances.
/// </summary>
public static class DbCommandRequestExtensions
{
    /// <summary>
    /// Clones the request while marking it as a stored procedure invocation.
    /// Optionally overrides the procedure name when you want shorthand builder calls before invoking
    /// <see cref="IDatabaseHelper.ExecuteAsync"/>, <see cref="IDatabaseHelper.Execute"/>, or the other top-level helpers.
    /// </summary>
    /// <param name="request">The original request.</param>
    /// <param name="storedProcedureName">Optional stored procedure name. Defaults to the original <see cref="DbCommandRequest.CommandText"/>.</param>
    /// <returns>A copy of the request configured to execute as <see cref="CommandType.StoredProcedure"/>.</returns>
    public static DbCommandRequest AsStoredProcedure(this DbCommandRequest request, string? storedProcedureName = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var commandText = storedProcedureName ?? request.CommandText;
        if (string.IsNullOrWhiteSpace(commandText))
        {
            throw new ArgumentException("Stored procedure name must be provided.", nameof(storedProcedureName));
        }

        return new DbCommandRequest
        {
            CommandText = commandText,
            CommandType = CommandType.StoredProcedure,
            Parameters = request.Parameters,
            CommandTimeoutSeconds = request.CommandTimeoutSeconds,
            PrepareCommand = request.PrepareCommand,
            Connection = request.Connection,
            CloseConnection = request.CloseConnection,
            Transaction = request.Transaction,
            OverrideOptions = request.OverrideOptions,
            CommandBehavior = request.CommandBehavior,
            TraceName = request.TraceName
        };
    }
}
