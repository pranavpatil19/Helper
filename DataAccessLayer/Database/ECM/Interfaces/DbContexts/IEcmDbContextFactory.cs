using System.Threading;
using System.Threading.Tasks;
namespace DataAccessLayer.Database.ECM.DbContexts;

public interface IEcmDbContextFactory
{
    Task<EcmDbContextBase> CreateDbContextAsync(CancellationToken cancellationToken = default);
    EcmDbContextBase CreateDbContext();
}
