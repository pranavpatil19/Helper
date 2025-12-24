using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using Microsoft.Extensions.Logging;
using Shared.Configuration;
using System.Threading.Tasks.Sources;

namespace DataAccessLayer.Transactions;

/// <summary>
/// Coordinates the creation of provider-aware transaction scopes.
/// </summary>
public sealed class TransactionManager : ITransactionManager
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISavepointManager _savepointManager;
    private readonly DatabaseOptions _defaultOptions;
    private readonly ILogger<TransactionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IResilienceStrategy _resilience;

    public TransactionManager(
        IDbConnectionFactory connectionFactory,
        ISavepointManager savepointManager,
        DatabaseOptions defaultOptions,
        IResilienceStrategy resilience,
        ILogger<TransactionManager> logger,
        ILoggerFactory loggerFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _savepointManager = savepointManager ?? throw new ArgumentNullException(nameof(savepointManager));
        _defaultOptions = defaultOptions ?? throw new ArgumentNullException(nameof(defaultOptions));
        _resilience = resilience ?? throw new ArgumentNullException(nameof(resilience));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public ValueTask<ITransactionScope> BeginAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        DatabaseOptions? options = null,
        TransactionScopeOption scopeOption = TransactionScopeOption.Required,
        CancellationToken cancellationToken = default)
    {
        TransactionScopeAmbient.EnsureInitialized();
        var scopeTask = BeginAsyncCore(isolationLevel, options, scopeOption, cancellationToken);
        if (scopeTask.IsCompletedSuccessfully)
        {
            return new ValueTask<ITransactionScope>(EnterAmbientIfNeeded(scopeTask.Result));
        }

        var source = new AmbientScopeValueTaskSource(scopeTask);
        return new ValueTask<ITransactionScope>(source, source.Version);
    }

    public ITransactionScope Begin(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        DatabaseOptions? options = null,
        TransactionScopeOption scopeOption = TransactionScopeOption.Required)
    {
        return scopeOption switch
        {
            TransactionScopeOption.Required => BeginRequired(isolationLevel, options),
            TransactionScopeOption.RequiresNew => BeginNew(isolationLevel, options),
            TransactionScopeOption.Suppress => BeginSuppressed(options),
            _ => throw new ArgumentOutOfRangeException(nameof(scopeOption))
        };
    }

    private async Task<ITransactionScope> BeginAsyncCore(
        IsolationLevel isolationLevel,
        DatabaseOptions? options,
        TransactionScopeOption scopeOption,
        CancellationToken cancellationToken)
    {
        return scopeOption switch
        {
            TransactionScopeOption.Required => await BeginRequiredAsync(isolationLevel, options, cancellationToken).ConfigureAwait(false),
            TransactionScopeOption.RequiresNew => await BeginNewAsync(isolationLevel, options, cancellationToken).ConfigureAwait(false),
            TransactionScopeOption.Suppress => await BeginSuppressedAsync(options, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(scopeOption))
        };
    }

    private async Task<ITransactionScope> BeginRequiredAsync(
        IsolationLevel isolationLevel,
        DatabaseOptions? options,
        CancellationToken cancellationToken)
    {
        var ambient = TransactionScopeAmbient.Current;
        if (ambient is not null && ambient is not SuppressedTransactionScope)
        {
            return new DependentTransactionScope(ambient);
        }

        return await BeginNewAsync(isolationLevel, options, cancellationToken).ConfigureAwait(false);
    }

    private ITransactionScope BeginRequired(
        IsolationLevel isolationLevel,
        DatabaseOptions? options)
    {
        var ambient = TransactionScopeAmbient.Current;
        if (ambient is not null && ambient is not SuppressedTransactionScope)
        {
            return new DependentTransactionScope(ambient);
        }

        return EnterAmbientIfNeeded(BeginNew(isolationLevel, options));
    }

    private async Task<ITransactionScope> BeginNewAsync(
        IsolationLevel isolationLevel,
        DatabaseOptions? options,
        CancellationToken cancellationToken)
    {
        var transactionOptions = options ?? _defaultOptions;
        var connection = _connectionFactory.CreateConnection(transactionOptions);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var transaction = await BeginTransactionAsync(connection, isolationLevel, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Began transaction ({Isolation}) for provider {Provider}.", isolationLevel, transactionOptions.Provider);
        var scopeLogger = _loggerFactory.CreateLogger<TransactionScope>();
        return EnterAmbientIfNeeded(new TransactionScope(connection, transaction, transactionOptions, _savepointManager, _resilience, scopeLogger));
    }

    private ITransactionScope BeginNew(
        IsolationLevel isolationLevel,
        DatabaseOptions? options)
    {
        var transactionOptions = options ?? _defaultOptions;
        var connection = _connectionFactory.CreateConnection(transactionOptions);
        connection.OpenAsync(CancellationToken.None).GetAwaiter().GetResult();
        var transaction = BeginTransactionAsync(connection, isolationLevel, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        _logger.LogDebug("Began transaction ({Isolation}) for provider {Provider}.", isolationLevel, transactionOptions.Provider);
        var scopeLogger = _loggerFactory.CreateLogger<TransactionScope>();
        return EnterAmbientIfNeeded(new TransactionScope(connection, transaction, transactionOptions, _savepointManager, _resilience, scopeLogger));
    }

    private async Task<ITransactionScope> BeginSuppressedAsync(
        DatabaseOptions? options,
        CancellationToken cancellationToken)
    {
        var scope = await SuppressedTransactionScope.CreateAsync(_connectionFactory, options ?? _defaultOptions, cancellationToken)
            .ConfigureAwait(false);
        return EnterAmbientIfNeeded(scope);
    }

    private ITransactionScope BeginSuppressed(DatabaseOptions? options)
    {
        return EnterAmbientIfNeeded(SuppressedTransactionScope.Create(_connectionFactory, options ?? _defaultOptions));
    }

    private static ValueTask<DbTransaction> BeginTransactionAsync(
        DbConnection connection,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken) =>
        connection.BeginTransactionAsync(isolationLevel, cancellationToken);

    private static TScope EnterAmbientIfNeeded<TScope>(TScope scope)
        where TScope : ITransactionScope
    {
        if (scope is IAmbientScope ambient)
        {
            ambient.EnterAmbient();
        }

        return scope;
    }

    private sealed class AmbientScopeValueTaskSource : IValueTaskSource<ITransactionScope>
    {
        private ManualResetValueTaskSourceCore<ITransactionScope> _core;

        public AmbientScopeValueTaskSource(
            Task<ITransactionScope> innerTask)
        {
            _core = new ManualResetValueTaskSourceCore<ITransactionScope>
            {
                RunContinuationsAsynchronously = true
            };
            _ = AwaitInnerAsync(innerTask);
        }

        public short Version => _core.Version;

        public ITransactionScope GetResult(short token)
        {
            var scope = _core.GetResult(token);
            return EnterAmbientIfNeeded(scope);
        }

        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            _core.OnCompleted(continuation, state, token, flags);

        private async Task AwaitInnerAsync(Task<ITransactionScope> innerTask)
        {
            try
            {
                var scope = await innerTask.ConfigureAwait(false);
                _core.SetResult(scope);
            }
            catch (Exception ex)
            {
                _core.SetException(ex);
            }
        }
    }
}
