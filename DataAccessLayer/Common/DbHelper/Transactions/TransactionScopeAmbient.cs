using System;
using System.Collections.Generic;
using System.Threading;

namespace DataAccessLayer.Transactions;

internal static class TransactionScopeAmbient
{
    private sealed class AmbientToken : IDisposable
    {
        private readonly AmbientHolder _ambientHolder;
        private bool _disposed;

        public AmbientToken(AmbientHolder holder)
        {
            _ambientHolder = holder;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_ambientHolder.Stack.Count > 0)
            {
                _ambientHolder.Stack.Pop();
            }

            _disposed = true;
        }
    }

    private sealed class AmbientHolder
    {
        public Stack<ITransactionScope> Stack { get; } = new();
    }

    private static readonly AsyncLocal<AmbientHolder?> _holder = new();

    public static ITransactionScope? Current
    {
        get
        {
            var holder = _holder.Value;
            return holder is { Stack.Count: > 0 } ? holder.Stack.Peek() : null;
        }
    }

    public static void EnsureInitialized()
    {
        _holder.Value ??= new AmbientHolder();
    }

    private static AmbientHolder GetOrCreateHolder() => _holder.Value ??= new AmbientHolder();

    public static IDisposable Push(ITransactionScope scope)
    {
        var holder = GetOrCreateHolder();
        holder.Stack.Push(scope);
        return new AmbientToken(holder);
    }
}

internal interface IAmbientScope
{
    void EnterAmbient();
}
