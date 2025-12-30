using System.Threading;
using System.Threading.Tasks;

namespace CoreBusiness.Workflows;

public interface IMigrationWorkflow
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
