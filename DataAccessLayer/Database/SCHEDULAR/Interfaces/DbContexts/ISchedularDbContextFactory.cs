using System.Threading;
using System.Threading.Tasks;
namespace DataAccessLayer.Database.SCHEDULAR.DbContexts;

public interface ISchedularDbContextFactory
{
    Task<SchedularDbContextBase> CreateDbContextAsync(CancellationToken cancellationToken = default);
    SchedularDbContextBase CreateDbContext();
}
