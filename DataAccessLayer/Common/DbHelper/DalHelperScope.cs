using System;
using DataAccessLayer.Execution;
using DataAccessLayer.Transactions;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Represents a scoped set of DAL services created on demand (helper + transaction manager).
/// <see cref="DalHelperFactory"/> is the preferred entry point for creating instances.
/// Dispose (or <c>await using</c>) the scope to release all resources when finished.
/// </summary>
public sealed class DalHelperScope : IDisposable, IAsyncDisposable
{
    private readonly IServiceProvider _rootProvider;
    private readonly AsyncServiceScope _scope;
    private bool _disposed;

    internal DalHelperScope(IServiceProvider rootProvider, AsyncServiceScope scope)
    {
        _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
        _scope = scope;
        DatabaseHelper = scope.ServiceProvider.GetRequiredService<IDatabaseHelper>();
        TransactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
    }

    /// <summary>
    /// Gets the provider-specific database helper for executing commands.
    /// </summary>
    public IDatabaseHelper DatabaseHelper { get; }

    /// <summary>
    /// Gets the transaction manager that shares the same scoped service graph.
    /// </summary>
    public ITransactionManager TransactionManager { get; }

    /// <summary>
    /// Gets the scoped service provider for resolving optional DAL services.
    /// </summary>
    public IServiceProvider Services => _scope.ServiceProvider;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _scope.Dispose();
        if (_rootProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _scope.DisposeAsync().ConfigureAwait(false);
        switch (_rootProvider)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        _disposed = true;
    }
}
