using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Database.SCHEDULAR.DbContexts;

/// <summary>
/// Bridges EF Core's <see cref="IDbContextFactory{TContext}"/> with DAL abstractions so the rest
/// of the code base can request <see cref="ISchedularDbContextFactory"/> without knowing which provider
/// (SQL Server, PostgreSQL, Oracle) was registered.
/// </summary>
public sealed class SchedularDbContextFactoryAdapter<TContext>(
    IDbContextFactory<TContext> innerFactory) : ISchedularDbContextFactory
    where TContext : SchedularDbContextBase
{
    public async Task<SchedularDbContextBase> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return await innerFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    }

    public SchedularDbContextBase CreateDbContext()
    {
        return innerFactory.CreateDbContext();
    }
}
