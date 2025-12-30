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
/// Oracle implementation of <see cref="IDalMigration"/> that wires the <c>CreateStagingTable</c> procedure.
/// </summary>
public sealed class OracleDalMigration : IDalMigration
{
    private const string ProcedureName = "CreateStagingTable";

    private readonly DatabaseOptions _databaseOptions;
    private readonly ILogger<OracleDalMigration> _logger;

    public OracleDalMigration(
        DatabaseOptions databaseOptions,
        ILogger<OracleDalMigration> logger)
    {
        _databaseOptions = databaseOptions ?? throw new ArgumentNullException(nameof(databaseOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task CreateStagingTableAsync(CancellationToken cancellationToken = default)
    {
        var provider = DatabaseProvider.Oracle;
        _logger.LogInformation("Starting {Provider} CreateStagingTable migration.", provider);
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
            throw new InvalidOperationException("Failed to create Oracle staging table.", ex);
        }
    }
}
