using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Database.ECM.DbContexts;

namespace DataAccessLayer.Database.ECM.Interfaces;

public interface IEcmDbContextFactory
{
    Task<EcmDbContextBase> CreateDbContextAsync(CancellationToken cancellationToken = default);
    EcmDbContextBase CreateDbContext();
}
