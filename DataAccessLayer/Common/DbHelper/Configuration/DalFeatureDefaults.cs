using System;
using System.Threading;
using Shared.Configuration;

namespace DataAccessLayer.Configuration;

/// <summary>
/// Central place to adjust default DAL feature toggles. Edit <see cref="CreateDefaultFeatures"/> when you need to
/// enable/disable subsystems globally (telemetry, bulk, EF helpers, etc.).
/// </summary>
public static class DalFeatureDefaults
{
    private static Func<DatabaseOptions, DalFeatures>? _overrideResolver;

    /// <summary>
    /// Resolves the feature manifest that will be applied when services are registered.
    /// </summary>
    public static DalFeatures Resolve(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var resolver = _overrideResolver;
        return resolver?.Invoke(options) ?? CreateDefaultFeatures(options);
    }

    /// <summary>
    /// Test-only escape hatch that temporarily overrides the resolver. Consumers should edit
    /// <see cref="CreateDefaultFeatures"/> instead of calling this directly.
    /// </summary>
    internal static IDisposable Override(Func<DatabaseOptions, DalFeatures> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var previous = Interlocked.Exchange(ref _overrideResolver, resolver);
        return new OverrideScope(previous);
    }

    private static DalFeatures CreateDefaultFeatures(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        // Customize features per provider (or environment) here if needed.
        return DalFeatures.Default;
    }

    private sealed class OverrideScope : IDisposable
    {
        private readonly Func<DatabaseOptions, DalFeatures>? _previous;
        private bool _disposed;

        public OverrideScope(Func<DatabaseOptions, DalFeatures>? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Interlocked.Exchange(ref _overrideResolver, _previous);
            _disposed = true;
        }
    }
}
