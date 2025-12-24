using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Database.ECM.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Database.ECM.DbContexts;

/// <summary>
/// Bridges EF Core's <see cref="IDbContextFactory{TContext}"/> with DAL abstractions so the rest
/// of the code base can request <see cref="IEcmDbContextFactory"/> without knowing which provider
/// (SQL Server, PostgreSQL, Oracle) was registered.
/// </summary>
public sealed class EcmDbContextFactoryAdapter<TContext>(
    IDbContextFactory<TContext> innerFactory) : IEcmDbContextFactory
    where TContext : EcmDbContextBase
{
    public async Task<EcmDbContextBase> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return await innerFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    }

    public EcmDbContextBase CreateDbContext()
    {
        return innerFactory.CreateDbContext();
    }
}
