using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DataAccessLayer.EF;

/// <summary>
/// Logs EF Core command execution times and SQL text.
/// </summary>
public sealed class DbCommandLoggingInterceptor : DbCommandInterceptor
{
    private readonly ILogger<DbCommandLoggingInterceptor> _logger;
    private readonly ConcurrentDictionary<Guid, Stopwatch> _timers = new();

    public DbCommandLoggingInterceptor(ILogger<DbCommandLoggingInterceptor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        StartTimer(eventData.CommandId);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        StopTimer(eventData.CommandId, command);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        StartTimer(eventData.CommandId);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        StopTimer(eventData.CommandId, command);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        StartTimer(eventData.CommandId);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        StopTimer(eventData.CommandId, command);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        StartTimer(eventData.CommandId);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        StopTimer(eventData.CommandId, command);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        StartTimer(eventData.CommandId);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        StopTimer(eventData.CommandId, command);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        StartTimer(eventData.CommandId);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        StopTimer(eventData.CommandId, command);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void StartTimer(Guid id)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _timers[id] = Stopwatch.StartNew();
        }
    }

    private void StopTimer(Guid id, DbCommand command)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        if (_timers.TryRemove(id, out var stopwatch))
        {
            stopwatch.Stop();
            _logger.LogInformation("EF command executed in {Elapsed} ms: {Command}", stopwatch.ElapsedMilliseconds, command.CommandText);
        }
    }
}
