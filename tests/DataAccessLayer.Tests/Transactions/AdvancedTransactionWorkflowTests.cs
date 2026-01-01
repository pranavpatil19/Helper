using System;
using System.Threading.Tasks;
using DataAccessLayer.Exceptions;
using DataAccessLayer.Transactions;
using Xunit;

namespace DataAccessLayer.Tests.Transactions;

public sealed class AdvancedTransactionWorkflowTests
{
    [Fact]
    public async Task RequiresNew_Savepoint_Suppress_Workflow_BehavesAsDocumented()
    {
        var factory = new RecordingConnectionFactory();
        var savepoints = new RecordingSavepointManager();
        var manager = TransactionTestHelpers.CreateManager(factory, savepoints);

        // Step 1: Audit scope (RequiresNew) always commits.
        await using (var auditScope = await manager.BeginAsync(scopeOption: TransactionScopeOption.RequiresNew))
        {
            await auditScope.CommitAsync();
        }

        Assert.True(factory.Connections[0].LastTransaction?.Committed);

        // Step 2: Main business scope with savepoint.
        await using var mainScope = await manager.BeginAsync();
        await mainScope.BeginSavepointAsync("business");

        // Step 3: Suppressed scope for logging/telemetry.
        await using (var suppressed = await manager.BeginAsync(scopeOption: TransactionScopeOption.Suppress))
        {
            Assert.Null(suppressed.Transaction);
            await Assert.ThrowsAsync<TransactionFeatureNotSupportedException>(() => suppressed.BeginSavepointAsync("noop"));
            await suppressed.CommitAsync(); // no-op but should not throw
        }

        var suppressedConnection = factory.Connections[^1];
        Assert.True(suppressedConnection.IsDisposed);

        // Simulate failure after savepoint and ensure rollback kicks in.
        var failure = new InvalidOperationException("boom");
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await mainScope.RollbackToSavepointAsync("business");
            await mainScope.RollbackAsync();
            throw failure;
        });

        Assert.Contains("Begin:business", savepoints.Events);
        Assert.Contains("Rollback:business", savepoints.Events);

        var mainConnection = factory.Connections[1];
        Assert.False(mainConnection.LastTransaction?.Committed);
        Assert.True(mainConnection.LastTransaction?.RolledBack);
    }
}
