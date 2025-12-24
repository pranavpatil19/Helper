using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using Microsoft.Extensions.Logging;
using Polly;

namespace DataAccessLayer.Transactions;

/// <summary>
/// Default implementation of <see cref="IMultiDbTransactionScope"/>.
/// </summary>
internal sealed class MultiDbTransactionScope : IMultiDbTransactionScope
{
    private readonly IReadOnlyList<TransactionParticipant> _participants;
    private readonly ILogger _logger;
    private readonly IResilienceStrategy _resilience;
    private bool _completed;
    private bool _disposed;

    public MultiDbTransactionScope(
        IReadOnlyList<TransactionParticipant> participants,
        IResilienceStrategy resilience,
        ILogger logger)
    {
        _participants = participants ?? throw new ArgumentNullException(nameof(participants));
        _resilience = resilience ?? throw new ArgumentNullException(nameof(resilience));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<TransactionParticipant> Participants => _participants;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (_completed)
        {
            return;
        }

        try
        {
            foreach (var participant in _participants)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogDebug("Committing transaction for provider {Provider}.", participant.Options.Provider);
                await _resilience.TransactionAsyncPolicy.ExecuteAsync(
                    (_, token) => participant.Transaction.CommitAsync(token),
                    new Context(),
                    cancellationToken).ConfigureAwait(false);
            }

            _completed = true;
        }
        catch
        {
            await SafeRollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public void Commit()
    {
        EnsureNotDisposed();
        if (_completed)
        {
            return;
        }

        try
        {
            foreach (var participant in _participants)
            {
                _logger.LogDebug("Committing transaction for provider {Provider}.", participant.Options.Provider);
                _resilience.TransactionSyncPolicy.Execute(participant.Transaction.Commit);
            }

            _completed = true;
        }
        catch
        {
            Rollback();
            throw;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await SafeRollbackAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Rollback()
    {
        EnsureNotDisposed();
        SafeRollback();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (!_completed)
        {
            await RollbackAsync().ConfigureAwait(false);
        }

        await DisposeParticipantsAsync().ConfigureAwait(false);

        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_completed)
        {
            Rollback();
        }

        DisposeParticipants();

        _disposed = true;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MultiDbTransactionScope));
        }
    }

    private async Task SafeRollbackAsync(CancellationToken cancellationToken)
    {
        foreach (var participant in _participants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogWarning("Rolling back transaction for provider {Provider}.", participant.Options.Provider);
            await _resilience.TransactionAsyncPolicy.ExecuteAsync(
                (_, token) => participant.Transaction.RollbackAsync(token),
                new Context(),
                cancellationToken).ConfigureAwait(false);
        }

        _completed = true;
    }

    private void SafeRollback()
    {
        foreach (var participant in _participants)
        {
            _logger.LogWarning("Rolling back transaction for provider {Provider}.", participant.Options.Provider);
            _resilience.TransactionSyncPolicy.Execute(participant.Transaction.Rollback);
        }

        _completed = true;
    }

    private async Task DisposeParticipantsAsync()
    {
        foreach (var participant in _participants)
        {
            await participant.Transaction.DisposeAsync().ConfigureAwait(false);
            await participant.Connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void DisposeParticipants() =>
        DisposeParticipantsAsync().GetAwaiter().GetResult();
}
