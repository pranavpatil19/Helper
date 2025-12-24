using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Configuration;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Transactions;

/// <summary>
/// Issues provider-specific statements to manage savepoints.
/// </summary>
public sealed class SavepointManager : ISavepointManager
{
    private readonly ILogger<SavepointManager> _logger;

    /// <summary>
    /// Creates a savepoint manager that emits provider-specific statements via the supplied logger.
    /// </summary>
    public SavepointManager(ILogger<SavepointManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Begins a savepoint asynchronously using the provider-specific command text.
    /// </summary>
    /// <remarks>Callers typically invoke this when starting a nested unit of work within an existing transaction scope.</remarks>
    public Task BeginSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default) =>
        ExecuteAsync(transaction, BuildCommand(options.Provider, SavepointOperation.Begin, name), cancellationToken);

    /// <summary>
    /// Begins a savepoint synchronously using the provider-specific command text.
    /// </summary>
    public void BeginSavepoint(DbTransaction transaction, string name, DatabaseOptions options) =>
        Execute(transaction, BuildCommand(options.Provider, SavepointOperation.Begin, name));

    /// <summary>
    /// Rolls back to an existing savepoint asynchronously.
    /// </summary>
    public Task RollbackToSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default) =>
        ExecuteAsync(transaction, BuildCommand(options.Provider, SavepointOperation.Rollback, name), cancellationToken);

    /// <summary>
    /// Rolls back to an existing savepoint synchronously.
    /// </summary>
    public void RollbackToSavepoint(DbTransaction transaction, string name, DatabaseOptions options) =>
        Execute(transaction, BuildCommand(options.Provider, SavepointOperation.Rollback, name));

    /// <summary>
    /// Releases a savepoint asynchronously (providers that auto-release simply no-op).
    /// </summary>
    public Task ReleaseSavepointAsync(DbTransaction transaction, string name, DatabaseOptions options, CancellationToken cancellationToken = default)
    {
        var command = BuildCommand(options.Provider, SavepointOperation.Release, name);
        return command is null
            ? Task.CompletedTask
            : ExecuteAsync(transaction, command, cancellationToken);
    }

    /// <summary>
    /// Releases a savepoint synchronously (providers that auto-release simply no-op).
    /// </summary>
    public void ReleaseSavepoint(DbTransaction transaction, string name, DatabaseOptions options)
    {
        var command = BuildCommand(options.Provider, SavepointOperation.Release, name);
        if (command is null)
        {
            return;
        }

        Execute(transaction, command);
    }

    private static string? BuildCommand(DatabaseProvider provider, SavepointOperation operation, string name)
    {
        var safeName = SanitizeName(name);
        return provider switch
        {
            DatabaseProvider.SqlServer => operation switch
            {
                SavepointOperation.Begin => $"SAVE TRANSACTION {safeName}",
                SavepointOperation.Rollback => $"ROLLBACK TRANSACTION {safeName}",
                SavepointOperation.Release => null, // SQL Server releases on commit automatically
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            },
            DatabaseProvider.PostgreSql => operation switch
            {
                SavepointOperation.Begin => $"SAVEPOINT {safeName}",
                SavepointOperation.Rollback => $"ROLLBACK TO SAVEPOINT {safeName}",
                SavepointOperation.Release => $"RELEASE SAVEPOINT {safeName}",
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            },
            DatabaseProvider.Oracle => operation switch
            {
                SavepointOperation.Begin => $"SAVEPOINT {safeName}",
                SavepointOperation.Rollback => $"ROLLBACK TO SAVEPOINT {safeName}",
                SavepointOperation.Release => null, // Oracle automatically releases on commit
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            },
            _ => throw new TransactionFeatureNotSupportedException($"Provider '{provider}' does not support savepoints.")
        };
    }

    private async Task ExecuteAsync(DbTransaction transaction, string? commandText, CancellationToken cancellationToken)
    {
        if (commandText is null)
        {
            return;
        }

        using var command = CreateCommand(transaction, commandText);
        _logger.LogTrace("Executing savepoint command: {Command}", commandText);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private void Execute(DbTransaction transaction, string? commandText)
    {
        if (commandText is null)
        {
            return;
        }

        using var command = CreateCommand(transaction, commandText);
        _logger.LogTrace("Executing savepoint command: {Command}", commandText);
        command.ExecuteNonQuery();
    }

    private static DbCommand CreateCommand(DbTransaction transaction, string commandText)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var connection = transaction.Connection ?? throw new InvalidOperationException("Transaction is not associated with a connection.");
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        return command;
    }

    private static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Savepoint name must be provided.", nameof(name));
        }

        foreach (var ch in name)
        {
            if (!(char.IsLetterOrDigit(ch) || ch is '_' or '-'))
            {
                throw new ArgumentException("Savepoint names may only contain letters, digits, '_' or '-'.", nameof(name));
            }
        }

        return name;
    }

    private enum SavepointOperation
    {
        Begin,
        Rollback,
        Release
    }
}
