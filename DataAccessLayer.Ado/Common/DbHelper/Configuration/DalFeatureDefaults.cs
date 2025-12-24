using System;
using System.Threading;
using Shared.Configuration;

namespace DataAccessLayer.Configuration;

public static class DalFeatureDefaults
{
    private static Func<DatabaseOptions, DalFeatures>? _overrideResolver;

    public static DalFeatures Resolve(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var resolver = _overrideResolver;
        return resolver?.Invoke(options) ?? CreateDefaultFeatures(options);
    }

    internal static IDisposable Override(Func<DatabaseOptions, DalFeatures> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var previous = Interlocked.Exchange(ref _overrideResolver, resolver);
        return new OverrideScope(previous);
    }

    private static DalFeatures CreateDefaultFeatures(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
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
