using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Database.ECM.Interfaces.Migration;
using DataAccessLayer.Execution;
using DataAccessLayer.Transactions;
using Microsoft.Extensions.Logging;
using Shared.Configuration;

namespace DataAccessLayer.Database.ECM.Services.Migration;

/// <summary>
/// PostgreSQL implementation of <see cref="IDalMigration"/> that executes the <c>create_staging_table</c> procedure.
/// </summary>
public sealed class PostgresDalMigration : IDalMigration
{
    private const string ProcedureName = "public.create_staging_table";

    private readonly DatabaseOptions _databaseOptions;
    private readonly ILogger<PostgresDalMigration> _logger;

    public PostgresDalMigration(
        DatabaseOptions databaseOptions,
        ILogger<PostgresDalMigration> logger)
    {
        _databaseOptions = databaseOptions ?? throw new ArgumentNullException(nameof(databaseOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task CreateStagingTableAsync(CancellationToken cancellationToken = default)
    {
        var provider = DatabaseProvider.PostgreSql;
        _logger.LogInformation("Starting {Provider} create_staging_table migration.", provider);

        try
        {
            await using var dal = DalHelperFactory.Create(
                _databaseOptions,
                provider,
                out var dbHelper,
                out var transactionManager);

            await transactionManager.WithTransactionAsync(
                async (scope, token) =>
                {
                    var proc = new DbCommandRequest
                    {
                        CommandText = ProcedureName,
                        CommandType = CommandType.StoredProcedure
                    }.WithScope(scope);

                    await dbHelper.ExecuteAsync(proc, token).ConfigureAwait(false);
                },
                isolationLevel: IsolationLevel.ReadCommitted,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully executed {Procedure} on {Provider}.", ProcedureName, provider);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to create PostgreSQL staging table.", ex);
        }
    }

}
