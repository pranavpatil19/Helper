using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Configuration;
using DataAccessLayer.Execution;
using DataAccessLayer.Providers.Oracle;
using DataAccessLayer.Providers.Postgres;
using Polly;
using FluentValidation;
using System.Linq;
using DataAccessLayer.Configuration;
using DataAccessLayer.Mapping;
using DataAccessLayer.Telemetry;
using DataAccessLayer.Exceptions;
using DataException = DataAccessLayer.Exceptions.DataException;
using Shared.IO;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Default implementation of <see cref="IDatabaseHelper"/> that normalizes provider-specific behaviors
/// (SQL Server, PostgreSQL, Oracle) and exposes a unified surface for executing commands, readers, and streams.
/// </summary>
public sealed partial class DatabaseHelper : IDatabaseHelper
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyOutputs =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));

    private readonly IConnectionScopeManager _connectionScopeManager;
    private readonly IDbCommandFactory _commandFactory;
    private readonly DatabaseOptions _defaultOptions;
    private readonly IResilienceStrategy _resilience;
    private readonly ILogger<DatabaseHelper> _logger;
    private readonly IValidator<DbCommandRequest>[] _requestValidators;
    private readonly IRowMapperFactory _rowMapperFactory;
    private readonly IDataAccessTelemetry _telemetry;
    private readonly DalRuntimeOptions _runtimeOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseHelper"/> class.
    /// </summary>
    /// <param name="connectionScopeManager">Factory responsible for leasing provider connections and ambient scopes.</param>
    /// <param name="commandFactory">Reusable DbCommand factory/parameter binder.</param>
    /// <param name="options">Default database options (provider, connection string, diagnostics).</param>
    /// <param name="resilience">Resilience strategy (retry policies).</param>
    /// <param name="logger">Structured logger used for telemetry.</param>
    public DatabaseHelper(
        IConnectionScopeManager connectionScopeManager,
        IDbCommandFactory commandFactory,
        DatabaseOptions options,
        IResilienceStrategy resilience,
        ILogger<DatabaseHelper> logger,
        IDataAccessTelemetry telemetry,
        DalRuntimeOptions runtimeOptions,
        IEnumerable<IValidator<DbCommandRequest>> requestValidators,
        IRowMapperFactory rowMapperFactory)
    {
        _connectionScopeManager = connectionScopeManager ?? throw new ArgumentNullException(nameof(connectionScopeManager));
        _commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
        _defaultOptions = options ?? throw new ArgumentNullException(nameof(options));
        _resilience = resilience ?? throw new ArgumentNullException(nameof(resilience));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _runtimeOptions = runtimeOptions ?? throw new ArgumentNullException(nameof(runtimeOptions));
        _requestValidators = requestValidators?.ToArray() ?? Array.Empty<IValidator<DbCommandRequest>>();
        _rowMapperFactory = rowMapperFactory ?? throw new ArgumentNullException(nameof(rowMapperFactory));
    }

    private Activity? StartActivity(string operation, DbCommandRequest request) =>
        _telemetry.StartCommandActivity(operation, request, _defaultOptions);

    private void RecordActivityResult(Activity? activity, DbExecutionResult execution) =>
        _telemetry.RecordCommandResult(activity, execution);

    #region Command Execution Infrastructure

    /// <summary>
    /// Executes a command that returns an optional scalar and rows affected, packaging outputs into <see cref="DbExecutionResult"/>.
    /// </summary>
    private async Task<DbExecutionResult> ExecuteScalarResultAsync(
        DbCommandRequest request,
        Func<DbCommand, CancellationToken, Task<CommandResult<object?>>> executor,
        CancellationToken cancellationToken)
    {
        var (result, outputs) = await ExecuteCoreAsync(request, executor, cancellationToken).ConfigureAwait(false);
        return new DbExecutionResult(result.RowsAffected, result.Data, outputs);
    }

    /// <summary>
    /// Synchronous companion to <see cref="ExecuteScalarResultAsync"/>.
    /// </summary>
    private DbExecutionResult ExecuteScalarResult(
        DbCommandRequest request,
        Func<DbCommand, CommandResult<object?>> executor)
    {
        var (result, outputs) = ExecuteCore(request, executor);
        return new DbExecutionResult(result.RowsAffected, result.Data, outputs);
    }

    /// <summary>
    /// Opens a scoped connection/command, logs execution, and runs the supplied delegate with proper disposal.
    /// </summary>
    private async Task<TResult> ExecuteCommandAsync<TResult>(
        DbCommandRequest request,
        Func<DbCommand, CancellationToken, Task<TResult>> executor,
        CancellationToken cancellationToken,
        string? labelOverride = null)
    {
        ValidateRequest(request);

        await using var scope = await AcquireConnectionScopeAsync(request, cancellationToken).ConfigureAwait(false);
        var command = await _commandFactory.GetCommandAsync(scope.Connection, request, cancellationToken).ConfigureAwait(false);
        ApplyScopedTransaction(request, scope, command);
        using var logScope = BeginLoggingScope(request);
        var stopwatch = Stopwatch.StartNew();
        var label = labelOverride ?? GetCommandLabel(request);

        try
        {
            var result = await executor(command, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            LogInformation("Executed command {Command} in {Elapsed} ms.", label, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Database command {Command} failed after {Elapsed} ms.", label, stopwatch.ElapsedMilliseconds);
            throw WrapException(request, ex);
        }
        finally
        {
            _commandFactory.ReturnCommand(command);
        }
    }

    /// <summary>
    /// Synchronous version of <see cref="ExecuteCommandAsync{TResult}"/>.
    /// </summary>
    private TResult ExecuteCommand<TResult>(
        DbCommandRequest request,
        Func<DbCommand, TResult> executor,
        string? labelOverride = null)
    {
        ValidateRequest(request);

        using var scope = AcquireConnectionScope(request);
        var command = _commandFactory.GetCommand(scope.Connection, request);
        ApplyScopedTransaction(request, scope, command);
        using var logScope = BeginLoggingScope(request);
        var stopwatch = Stopwatch.StartNew();
        var label = labelOverride ?? GetCommandLabel(request);

        try
        {
            var result = executor(command);
            stopwatch.Stop();
            LogInformation("Executed command {Command} in {Elapsed} ms.", label, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Database command {Command} failed after {Elapsed} ms.", label, stopwatch.ElapsedMilliseconds);
            throw WrapException(request, ex);
        }
        finally
        {
            _commandFactory.ReturnCommand(command);
        }
    }

    /// <summary>
    /// Executes a command via <see cref="ExecuteCommandAsync{TResult}"/> and captures output parameters.
    /// </summary>
    private Task<(CommandResult<T> Result, IReadOnlyDictionary<string, object?> Outputs)> ExecuteCoreAsync<T>(
        DbCommandRequest request,
        Func<DbCommand, CancellationToken, Task<CommandResult<T>>> executor,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            request,
            async (command, token) =>
            {
                var result = await executor(command, token).ConfigureAwait(false);
                var outputs = ExtractOutputs(command);
                return (result, outputs);
            },
            cancellationToken);

    /// <summary>
    /// Synchronous companion to <see cref="ExecuteCoreAsync{T}"/>.
    /// </summary>
    private (CommandResult<T> Result, IReadOnlyDictionary<string, object?> Outputs) ExecuteCore<T>(
        DbCommandRequest request,
        Func<DbCommand, CommandResult<T>> executor) =>
        ExecuteCommand(
            request,
            command =>
            {
                var result = executor(command);
                var outputs = ExtractOutputs(command);
                return (result, outputs);
            });

    #endregion

    #region Buffered Materialization Helpers

    /// <summary>
    /// Executes a buffered query (reader fully consumed) and applies telemetry for the number of produced records.
    /// </summary>
    private Task<DbQueryResult<TResult>> ExecuteBufferedQueryAsync<TResult>(
        string operation,
        DbCommandRequest request,
        Func<DbCommand, CancellationToken, Task<CommandResult<TResult>>> executor,
        Func<TResult, int> recordCounter,
        CancellationToken cancellationToken) =>
        ExecuteWithRequestActivityAsync(
            operation,
            request,
            async () =>
            {
                var (result, outputs) = await ExecuteWithResilienceAsync(
                    token => ExecuteCoreAsync(request, executor, token),
                    cancellationToken).ConfigureAwait(false);

                var execution = new DbExecutionResult(result.RowsAffected, null, outputs);
                return new DbQueryResult<TResult>(result.Data, execution);
            },
            (activity, queryResult) =>
            {
                _telemetry.RecordCommandResult(activity, queryResult.Execution, recordCounter(queryResult.Data));
            });

    /// <summary>
    /// Synchronous counterpart to <see cref="ExecuteBufferedQueryAsync{TResult}"/>.
    /// </summary>
    private DbQueryResult<TResult> ExecuteBufferedQuery<TResult>(
        string operation,
        DbCommandRequest request,
        Func<DbCommand, CommandResult<TResult>> executor,
        Func<TResult, int> recordCounter) =>
        ExecuteWithRequestActivity(
            operation,
            request,
            () =>
            {
                var (result, outputs) = ExecuteWithResilience(() => ExecuteCore(request, executor));
                var execution = new DbExecutionResult(result.RowsAffected, null, outputs);
                return new DbQueryResult<TResult>(result.Data, execution);
            },
            (activity, queryResult) =>
            {
                _telemetry.RecordCommandResult(activity, queryResult.Execution, recordCounter(queryResult.Data));
            });

    private static async Task<DataSet> ReadDataSetAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var dataSet = new DataSet();
        var tableIndex = 0;
        do
        {
            var table = await MaterializeTableAsync(reader, $"Table{tableIndex++}", cancellationToken).ConfigureAwait(false);
            dataSet.Tables.Add(table);
        }
        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));

        return dataSet;
    }

    private static DataSet ReadDataSet(DbDataReader reader)
    {
        var dataSet = new DataSet();
        var tableIndex = 0;
        do
        {
            var table = MaterializeTable(reader, $"Table{tableIndex++}");
            dataSet.Tables.Add(table);
        }
        while (reader.NextResult());

        return dataSet;
    }

    private static async Task<DataTable> MaterializeTableAsync(DbDataReader reader, string tableName, CancellationToken cancellationToken)
    {
        var table = CreateSchemaTable(reader, tableName);
        var buffer = new object?[reader.FieldCount];

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            PopulateBuffer(reader, buffer);
            AddRow(table, buffer);
        }

        return table;
    }

    private static DataTable MaterializeTable(DbDataReader reader, string tableName)
    {
        var table = CreateSchemaTable(reader, tableName);
        var buffer = new object?[reader.FieldCount];

        while (reader.Read())
        {
            PopulateBuffer(reader, buffer);
            AddRow(table, buffer);
        }

        return table;
    }

    private static DataTable CreateSchemaTable(DbDataReader reader, string tableName)
    {
        var table = new DataTable(tableName);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            if (table.Columns.Contains(columnName))
            {
                columnName = $"{columnName}_{i}";
            }

            var fieldType = reader.GetFieldType(i) ?? typeof(object);
            table.Columns.Add(columnName, fieldType);
        }

        return table;
    }

    private static void AddRow(DataTable table, object?[] values)
    {
        var row = table.NewRow();
        for (var i = 0; i < values.Length; i++)
        {
            row[i] = values[i] ?? DBNull.Value;
        }

        table.Rows.Add(row);
    }

    private static void PopulateBuffer(DbDataReader reader, object?[] buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }
    }

    #endregion

    #region Request & Stored Procedure Helpers

    /// <summary>
    /// Applies basic guardrails (null/text validation plus FluentValidation pipeline) to every <see cref="DbCommandRequest"/>.
    /// </summary>
    private void ValidateRequest(DbCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CommandText))
        {
            throw new ArgumentException("Command text must be provided.", nameof(request));
        }

        if (!request.Validate)
        {
            return;
        }

        foreach (var validator in _requestValidators)
        {
            validator.ValidateAndThrow(request);
        }
    }

    private static DbCommandRequest CreateStoredProcedureRequest(
        string procedureName,
        IReadOnlyList<DbParameterDefinition>? parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
        return new DbCommandRequest
        {
            CommandText = procedureName,
            CommandType = CommandType.StoredProcedure,
            Parameters = parameters ?? Array.Empty<DbParameterDefinition>()
        };
    }

    private async Task<TResult> ExecuteStoredProcedureRequestAsync<TResult>(
        string procedureName,
        IReadOnlyList<DbParameterDefinition>? parameters,
        Func<DbCommandRequest, CancellationToken, Task<TResult>> executor,
        CancellationToken cancellationToken)
    {
        var request = CreateStoredProcedureRequest(procedureName, parameters);
        try
        {
            return await executor(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapException(request, ex);
        }
    }

    private TResult ExecuteStoredProcedureRequest<TResult>(
        string procedureName,
        IReadOnlyList<DbParameterDefinition>? parameters,
        Func<DbCommandRequest, TResult> executor)
    {
        var request = CreateStoredProcedureRequest(procedureName, parameters);
        try
        {
            return executor(request);
        }
        catch (Exception ex)
        {
            throw WrapException(request, ex);
        }
    }
    #endregion

    #region Parameter & Output Handling

    private static IReadOnlyDictionary<string, object?> ExtractOutputs(DbCommand command)
    {
        if (command.Parameters.Count == 0)
        {
            return EmptyOutputs;
        }

        Dictionary<string, object?>? buffer = null;
        foreach (DbParameter parameter in command.Parameters)
        {
            if (parameter.Direction == ParameterDirection.Input)
            {
                continue;
            }

            buffer ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var key = TrimPrefix(parameter.ParameterName);
            var value = parameter.Value is DBNull ? null : parameter.Value;
            buffer[key] = value;
        }

        return buffer is null ? EmptyOutputs : new ReadOnlyDictionary<string, object?>(buffer);
    }

    private static string TrimPrefix(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        return name[0] is '@' or ':' or '?' ? name[1..] : name;
    }

    #endregion

    #region Provider Execution Paths

    private ValueTask<int> ExecuteNonQueryWithFallbackAsync(DbCommandRequest request, DbCommand command, CancellationToken cancellationToken)
    {
        if (ShouldUseSynchronousProviderPath(request))
        {
            return ValueTask.FromResult(command.ExecuteNonQuery());
        }

        return new ValueTask<int>(command.ExecuteNonQueryAsync(cancellationToken));
    }

    private ValueTask<object?> ExecuteScalarWithFallbackAsync(DbCommandRequest request, DbCommand command, CancellationToken cancellationToken)
    {
        if (ShouldUseSynchronousProviderPath(request))
        {
            return ValueTask.FromResult(command.ExecuteScalar());
        }

        return new ValueTask<object?>(command.ExecuteScalarAsync(cancellationToken));
    }

    private ValueTask<DbDataReader> ExecuteReaderWithFallbackAsync(
        DbCommandRequest request,
        DbCommand command,
        CommandBehavior behavior,
        CancellationToken cancellationToken)
    {
        if (ShouldUseSynchronousProviderPath(request))
        {
            return ValueTask.FromResult(command.ExecuteReader(behavior));
        }

        return new ValueTask<DbDataReader>(command.ExecuteReaderAsync(behavior, cancellationToken));
    }

    // Oracle's managed provider still executes async members synchronously. We short-circuit
    // to the sync path to avoid bogus async usage and reduce Task allocations.
    private bool ShouldUseSynchronousProviderPath(DbCommandRequest request) =>
        ResolveProvider(request) == DatabaseProvider.Oracle;

    private DatabaseProvider ResolveProvider(DbCommandRequest request) =>
        (request.OverrideOptions ?? _defaultOptions).Provider;

    private bool ShouldWrapExceptions(DbCommandRequest request) =>
        (request.OverrideOptions?.WrapProviderExceptions) ?? _defaultOptions.WrapProviderExceptions;

    private Exception WrapException(DbCommandRequest request, Exception exception)
    {
        if (!ShouldWrapExceptions(request) || exception is DataException)
        {
            return exception;
        }

        return new DataException($"Database command '{GetCommandLabel(request)}' failed.", exception);
    }

    #endregion

    #region Activity Helpers

    private Task<T> WrapExecutionAsync<T>(
        DbCommandRequest request,
        string operation,
        Func<Task<T>> action,
        Action<Activity?, T>? onCompleted = null) =>
        ExecuteWithRequestActivityAsync(operation, request, action, onCompleted);

    private T WrapExecution<T>(
        DbCommandRequest request,
        string operation,
        Func<T> action,
        Action<Activity?, T>? onCompleted = null) =>
        ExecuteWithRequestActivity(operation, request, action, onCompleted);

    private async Task<T> ExecuteWithRequestActivityAsync<T>(
        string operation,
        DbCommandRequest request,
        Func<Task<T>> action,
        Action<Activity?, T>? onCompleted = null)
    {
        ValidateRequest(request);
        using var activity = StartActivity(operation, request);
        try
        {
            var result = await action().ConfigureAwait(false);
            onCompleted?.Invoke(activity, result);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw WrapException(request, ex);
        }
    }

    #endregion

    #region Provider-Specific Execution Plans

    private T ExecuteWithRequestActivity<T>(
        string operation,
        DbCommandRequest request,
        Func<T> action,
        Action<Activity?, T>? onCompleted = null)
    {
        ValidateRequest(request);
        using var activity = StartActivity(operation, request);
        try
        {
            var result = action();
            onCompleted?.Invoke(activity, result);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw WrapException(request, ex);
        }
    }

    private bool TryExecutePostgresOutParametersAsync(
        DbCommandRequest request,
        bool expectScalar,
        CancellationToken cancellationToken,
        out Task<DbExecutionResult> task)
    {
        // PostgreSQL lacks native OUT parameters, so we rewrite the invocation into
        // a SELECT plan and hydrate outputs from the resulting single row.
        if (!PostgresOutParameterPlan.TryCreate(request, ResolveProvider(request), out var plan))
        {
            task = default!;
            return false;
        }

        task = ExecuteWithResilienceAsync(
            token => ExecutePostgresPlanAsync(plan!, expectScalar, token),
            cancellationToken);
        return true;
    }

    private bool TryExecutePostgresOutParameters(
        DbCommandRequest request,
        bool expectScalar,
        out DbExecutionResult result)
    {
        if (!PostgresOutParameterPlan.TryCreate(request, ResolveProvider(request), out var plan))
        {
            result = default!;
            return false;
        }

        result = ExecuteWithResilience(() => ExecutePostgresPlan(plan!, expectScalar));
        return true;
    }

    private Task<DbExecutionResult> ExecutePostgresPlanAsync(
        PostgresOutParameterPlan plan,
        bool expectScalar,
        CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            plan.Request,
            async (command, token) =>
            {
                await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, token).ConfigureAwait(false);
                if (!await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    throw new ProviderFeatureException("PostgreSQL OUT parameter emulation expected a result row but none was returned.");
                }

                var outputs = plan.ReadOutputs(reader);
                var data = expectScalar ? plan.GetScalar(reader) : null;
                return new DbExecutionResult(reader.RecordsAffected, data, outputs);
            },
            cancellationToken,
            plan.Request.TraceName ?? plan.Request.CommandText);

    private DbExecutionResult ExecutePostgresPlan(PostgresOutParameterPlan plan, bool expectScalar) =>
        ExecuteCommand(
            plan.Request,
            command =>
            {
                using var reader = command.ExecuteReader(CommandBehavior.SingleRow);
                if (!reader.Read())
                {
                    throw new ProviderFeatureException("PostgreSQL OUT parameter emulation expected a result row but none was returned.");
                }

                var outputs = plan.ReadOutputs(reader);
                var data = expectScalar ? plan.GetScalar(reader) : null;
                return new DbExecutionResult(reader.RecordsAffected, data, outputs);
            },
            plan.Request.TraceName ?? plan.Request.CommandText);

    // Oracle returns REF CURSORs via output parameters. We execute the procedure once, grab
    // the cursor parameter, and hand the underlying reader back to the caller inside a lease.
    private async Task<DbReaderScope> ExecuteRefCursorCoreAsync(
        DbCommandRequest request,
        string cursorParameterName,
        CancellationToken cancellationToken)
    {
        var scope = await AcquireConnectionScopeAsync(request, cancellationToken).ConfigureAwait(false);
        var command = await _commandFactory.GetCommandAsync(scope.Connection, request, cancellationToken).ConfigureAwait(false);
        ApplyScopedTransaction(request, scope, command);
        try
        {
            await ExecuteNonQueryWithFallbackAsync(request, command, cancellationToken).ConfigureAwait(false);
            var reader = GetOracleRefCursorReader(command, cursorParameterName);
            return new DbReaderScope(reader, command, scope, _commandFactory);
        }
        catch
        {
            _commandFactory.ReturnCommand(command);
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private DbReaderScope ExecuteRefCursorCore(
        DbCommandRequest request,
        string cursorParameterName)
    {
        var scope = AcquireConnectionScope(request);
        var command = _commandFactory.GetCommand(scope.Connection, request);
        ApplyScopedTransaction(request, scope, command);
        try
        {
            command.ExecuteNonQuery();
            var reader = GetOracleRefCursorReader(command, cursorParameterName);
            return new DbReaderScope(reader, command, scope, _commandFactory);
        }
        catch
        {
            _commandFactory.ReturnCommand(command);
            scope.Dispose();
            throw;
        }
    }

    private DbDataReader GetOracleRefCursorReader(DbCommand command, string cursorParameterName)
    {
        foreach (DbParameter parameter in command.Parameters)
        {
            var name = TrimPrefix(parameter.ParameterName);
            if (string.Equals(name, cursorParameterName, StringComparison.OrdinalIgnoreCase))
            {
                return OracleRefCursorReaderFactory.Create(parameter);
            }
        }

        throw new ProviderFeatureException($"Cursor parameter '{cursorParameterName}' was not found on the executed command.");
    }

    private void EnsureOracleProvider(DbCommandRequest request)
    {
        if (ResolveProvider(request) != DatabaseProvider.Oracle)
        {
            throw new ProviderNotSupportedException("REF CURSOR execution is only supported for Oracle providers.");
        }
    }

    #endregion

    #region Telemetry & Logging

    private IDisposable? BeginLoggingScope(DbCommandRequest request) =>
        ShouldLogDetails ? _logger.BeginScope("DatabaseCommand:{Name}", GetCommandLabel(request)) : null;

    private readonly record struct CommandResult<T>(T Data, int RowsAffected);

    /// <summary>
    /// Executes the supplied delegate under the configured resilience (retry/timeout) policy.
    /// </summary>
    private Task<TResult> ExecuteWithResilienceAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken)
        => _resilience.CommandAsyncPolicy.ExecuteAsync((_, token) => action(token), new Context(), cancellationToken);

    /// <summary>
    /// Synchronous counterpart to <see cref="ExecuteWithResilienceAsync{TResult}"/>.
    /// </summary>
    private TResult ExecuteWithResilience<TResult>(Func<TResult> action)
        => _resilience.CommandSyncPolicy.Execute(action);

    private Task<long> StreamBinaryInternalAsync(DbCommandRequest request, int ordinal, Stream destination, CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            request,
            async (command, token) =>
            {
                var behavior = DbStreamUtilities.EnsureSequentialBehavior(request.CommandBehavior);
                await using var reader = await ExecuteReaderWithFallbackAsync(request, command, behavior, token).ConfigureAwait(false);
                if (!await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    return 0;
                }

                await using var source = reader.GetStream(ordinal);
                var total = await DbStreamUtilities.CopyStreamAsync(source, destination, token).ConfigureAwait(false);
                LogInformation("Streamed {Bytes} bytes from {Command}.", total, GetCommandLabel(request));
                return total;
            },
            cancellationToken);

    private long StreamBinaryInternal(DbCommandRequest request, int ordinal, Stream destination) =>
        ExecuteCommand(
            request,
            command =>
            {
                var behavior = DbStreamUtilities.EnsureSequentialBehavior(request.CommandBehavior);
                using var reader = command.ExecuteReader(behavior);
                if (!reader.Read())
                {
                    return 0;
                }

                using var source = reader.GetStream(ordinal);
                var total = DbStreamUtilities.CopyStream(source, destination);
                LogInformation("Streamed {Bytes} bytes from {Command}.", total, GetCommandLabel(request));
                return total;
            });

    private Task<long> StreamTextInternalAsync(DbCommandRequest request, int ordinal, TextWriter writer, CancellationToken cancellationToken) =>
        ExecuteCommandAsync(
            request,
            async (command, token) =>
            {
                var behavior = DbStreamUtilities.EnsureSequentialBehavior(request.CommandBehavior);
                await using var reader = await ExecuteReaderWithFallbackAsync(request, command, behavior, token).ConfigureAwait(false);
                if (!await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    return 0;
                }

                using var textReader = reader.GetTextReader(ordinal);
                var total = await DbStreamUtilities.CopyTextAsync(textReader, writer, token).ConfigureAwait(false);
                LogInformation("Streamed {Chars} chars from {Command}.", total, GetCommandLabel(request));
                return total;
            },
            cancellationToken);

    private long StreamTextInternal(DbCommandRequest request, int ordinal, TextWriter writer) =>
        ExecuteCommand(
            request,
            command =>
            {
                var behavior = DbStreamUtilities.EnsureSequentialBehavior(request.CommandBehavior);
                using var reader = command.ExecuteReader(behavior);
                if (!reader.Read())
                {
                    return 0;
                }

                using var textReader = reader.GetTextReader(ordinal);
                var total = DbStreamUtilities.CopyText(textReader, writer);
                LogInformation("Streamed {Chars} chars from {Command}.", total, GetCommandLabel(request));
                return total;
            });

    private string GetCommandLabel(DbCommandRequest request) =>
        _telemetry.GetCommandDisplayName(request);

    private bool ShouldLogDetails => _runtimeOptions.EnableDetailedLogging && _logger.IsEnabled(LogLevel.Information);

    private void LogInformation(string message, params object?[] args)
    {
        if (ShouldLogDetails)
        {
            _logger.LogInformation(message, args);
        }
    }

    private Func<DbDataReader, T> ResolveRowMapper<T>(RowMapperRequest? mapperRequest)
        where T : class
    {
        var mapper = _rowMapperFactory.Create<T>(mapperRequest);
        return mapper.Map;
    }

    #endregion

    #region Scope Management

    private async ValueTask<ConnectionScope> AcquireConnectionScopeAsync(DbCommandRequest request, CancellationToken cancellationToken)
    {
        if (request.Connection is { } explicitConnection)
        {
            if (explicitConnection.State != ConnectionState.Open)
            {
                await explicitConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            return ConnectionScope.Wrap(explicitConnection, request.Transaction, request.CloseConnection);
        }

        return await _connectionScopeManager.LeaseAsync(request.OverrideOptions, cancellationToken).ConfigureAwait(false);
    }

    private ConnectionScope AcquireConnectionScope(DbCommandRequest request)
    {
        if (request.Connection is { } explicitConnection)
        {
            if (explicitConnection.State != ConnectionState.Open)
            {
                explicitConnection.Open();
            }

            return ConnectionScope.Wrap(explicitConnection, request.Transaction, request.CloseConnection);
        }

        return _connectionScopeManager.Lease(request.OverrideOptions);
    }

    private static void ApplyScopedTransaction(
        DbCommandRequest request,
        ConnectionScope scope,
        DbCommand command)
    {
        var transaction = request.Transaction ?? scope.Transaction;
        if (transaction is not null)
        {
            command.Transaction = transaction;
        }
    }

    #endregion
}
