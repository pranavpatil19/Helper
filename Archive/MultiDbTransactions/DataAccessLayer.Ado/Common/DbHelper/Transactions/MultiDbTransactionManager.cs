using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using Microsoft.Extensions.Logging;
using Shared.Configuration;

namespace DataAccessLayer.Transactions;

/// <summary>
/// Coordinates multi-database transactions without MSDTC.
/// </summary>
public sealed class MultiDbTransactionManager : IMultiDbTransactionManager
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<MultiDbTransactionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IResilienceStrategy _resilience;

    public MultiDbTransactionManager(
        IDbConnectionFactory connectionFactory,
        IResilienceStrategy resilience,
        ILogger<MultiDbTransactionManager> logger,
        ILoggerFactory loggerFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _resilience = resilience ?? throw new ArgumentNullException(nameof(resilience));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public async Task<IMultiDbTransactionScope> BeginAsync(
        IReadOnlyList<DatabaseOptions> options,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var participants = new List<TransactionParticipant>(options.Count);
        _logger.LogDebug("Beginning multi-db transaction scope for {Count} participants.", options.Count);

        try
        {
            foreach (var option in options)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var connection = _connectionFactory.CreateConnection(option);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                var transaction = await BeginTransactionAsync(connection, isolationLevel, cancellationToken).ConfigureAwait(false);
                participants.Add(new TransactionParticipant(option, connection, transaction));
            }

            var scopeLogger = _loggerFactory.CreateLogger<MultiDbTransactionScope>();
            return new MultiDbTransactionScope(participants, _resilience, scopeLogger);
        }
        catch
        {
            await DisposeParticipantsAsync(participants).ConfigureAwait(false);
            throw;
        }
    }

    public IMultiDbTransactionScope Begin(
        IReadOnlyList<DatabaseOptions> options,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted) =>
        BeginAsync(options, isolationLevel, CancellationToken.None).GetAwaiter().GetResult();

    private static void ValidateOptions(IReadOnlyList<DatabaseOptions> options)
    {
        if (options is null || options.Count == 0)
        {
            throw new ArgumentException("At least one database option is required.", nameof(options));
        }
    }

    private static ValueTask<DbTransaction> BeginTransactionAsync(
        DbConnection connection,
        IsolationLevel isolation,
        CancellationToken cancellationToken) =>
        connection.BeginTransactionAsync(isolation, cancellationToken);

    private static async Task DisposeParticipantsAsync(IEnumerable<TransactionParticipant> participants)
    {
        foreach (var participant in participants)
        {
            await participant.Transaction.DisposeAsync().ConfigureAwait(false);
            await participant.Connection.DisposeAsync().ConfigureAwait(false);
        }
    }

}
