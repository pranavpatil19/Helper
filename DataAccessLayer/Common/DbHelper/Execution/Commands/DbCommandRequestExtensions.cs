using System;
using System.Data;
using DataAccessLayer.Transactions;

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
            TraceName = request.TraceName ?? commandText
        };
    }
    /// <summary>
    /// Clones the request and binds it to the supplied <see cref="ITransactionScope"/> so the caller does not need
    /// to manually assign the connection/transaction or worry about closing scope-owned connections.
    /// </summary>
    /// <param name="request">Original request definition.</param>
    /// <param name="scope">Active transaction scope that owns the connection.</param>
    /// <param name="closeConnection">
    /// When <c>true</c>, the helper closes the scope connection after execution. Defaults to <c>false</c> because the scope controls disposal.
    /// </param>
    public static DbCommandRequest WithScope(this DbCommandRequest request, ITransactionScope scope, bool closeConnection = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(scope);

        return new DbCommandRequest
        {
            CommandText = request.CommandText,
            CommandType = request.CommandType,
            Parameters = request.Parameters,
            CommandTimeoutSeconds = request.CommandTimeoutSeconds,
            PrepareCommand = request.PrepareCommand,
            Connection = scope.Connection,
            CloseConnection = closeConnection,
            Transaction = scope.Transaction,
            OverrideOptions = request.OverrideOptions,
            CommandBehavior = request.CommandBehavior,
            TraceName = request.TraceName ?? request.CommandText,
            SkipValidation = request.SkipValidation
        };
    }
}
