using System.Threading;
using System.Threading.Tasks;

namespace CoreBusiness.Workflows;

public sealed class OracleMigrationWorkflow : IMigrationWorkflow
{
    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
