using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Polly;

namespace DataAccessLayer.EF;

/// <summary>
/// Helper extensions for sharing connections/transactions between EF Core and the DAL.
/// </summary>
public static class DbContextExtensions
{
    public static DbConnection GetOpenConnection(this DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        return connection;
    }

    public static async Task<DbConnection> GetOpenConnectionAsync(this DbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        return connection;
    }

    public static void UseAmbientTransaction(this DbContext context, ITransactionScope scope)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Transaction is null)
        {
            throw new InvalidOperationException("The current transaction scope is suppressed; there is no DbTransaction to share with EF Core.");
        }

        context.Database.UseTransaction(scope.Transaction);
    }

    public static DbCommandRequest WithAmbientConnection(this DbCommandRequest request, DbContext context, DbTransaction? transaction = null, bool closeConnection = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var connection = context.Database.GetDbConnection();
        var ambientTransaction = transaction ?? context.Database.CurrentTransaction?.GetDbTransaction();
        return new DbCommandRequest
        {
            CommandText = request.CommandText,
            CommandType = request.CommandType,
            Parameters = request.Parameters,
            CommandTimeoutSeconds = request.CommandTimeoutSeconds,
            PrepareCommand = request.PrepareCommand,
            Connection = connection,
            CloseConnection = closeConnection,
            Transaction = ambientTransaction,
            OverrideOptions = request.OverrideOptions,
            CommandBehavior = request.CommandBehavior,
            TraceName = request.TraceName
        };
    }

    public static Task<int> SaveChangesWithRetryAsync(this DbContext context, IResilienceStrategy resilience, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resilience);
        return resilience.TransactionAsyncPolicy.ExecuteAsync((_, token) => context.SaveChangesAsync(token), new Context(), cancellationToken);
    }

    public static int SaveChangesWithRetry(this DbContext context, IResilienceStrategy resilience)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resilience);
        return resilience.TransactionSyncPolicy.Execute(context.SaveChanges);
    }

    public static void TrackEntities<T>(this DbContext context, IEnumerable<T> entities, bool trackEntities = false)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entities);

        if (!trackEntities)
        {
            return;
        }

        foreach (var entity in entities)
        {
            if (entity is null)
            {
                continue;
            }

            var entry = context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                context.Attach(entity);
            }
        }
    }
}
