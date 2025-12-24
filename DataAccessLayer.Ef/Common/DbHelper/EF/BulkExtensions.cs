using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using DataAccessLayer.Execution;
using DataAccessLayer.Common.DbHelper;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configuration;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.EF;

/// <summary>
/// EF Core extensions that delegate to DAL bulk writers.
/// </summary>
public static class BulkExtensions
{
    public static Task WriteSqlServerBulkAsync<T>(
        this DbContext context,
        IEnumerable<T> rows,
        SqlServerBulkWriterOptions<T> options,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);

        var factory = services.GetRequiredService<IDbConnectionFactory>();
        var dbOptions = services.GetRequiredService<DatabaseOptions>();
        var clientFactory = services.GetRequiredService<ISqlBulkCopyClientFactory>();

        var writer = new SqlServerBulkWriter<T>(factory, dbOptions, options, clientFactory);
        return writer.WriteAsync(rows, cancellationToken);
    }
}
