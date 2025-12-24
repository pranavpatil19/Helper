using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace DataAccessLayer.Tests;

/// <summary>
/// Lightweight logger that captures log entries for assertions without touching global providers.
/// </summary>
public sealed class TestLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = new();

    /// <summary>
    /// Gets or sets whether the logger reports itself as enabled (defaults to true).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Captured log entries.
    /// </summary>
    public IReadOnlyList<LogEntry> Entries => _entries;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NullScope();

    public bool IsEnabled(LogLevel logLevel) => Enabled;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!Enabled)
        {
            return;
        }

        _entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    public readonly record struct LogEntry(LogLevel Level, string Message);

    private sealed class NullScope : IDisposable
    {
        public void Dispose() { }
    }
}
