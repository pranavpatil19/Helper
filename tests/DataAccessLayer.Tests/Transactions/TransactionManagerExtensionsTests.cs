using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Transactions;
using Xunit;

namespace DataAccessLayer.Tests.Transactions;

public sealed class TransactionManagerExtensionsTests
{
    [Fact]
    public async Task WithTransactionAsync_Commits_OnSuccess()
    {
        var factory = new RecordingConnectionFactory();
        var manager = TransactionTestHelpers.CreateManager(factory, new RecordingSavepointManager());

        await manager.WithTransactionAsync(
            async (scope, token) =>
            {
                Assert.NotNull(scope.Connection);
                Assert.NotNull(scope.Transaction);
                await Task.CompletedTask;
            },
            cancellationToken: CancellationToken.None);

        var connection = factory.Connections.Single();
        Assert.True(connection.LastTransaction?.Committed);
        Assert.False(connection.LastTransaction?.RolledBack);
    }

    [Fact]
    public async Task WithTransactionAsync_RollsBack_OnFailure()
    {
        var factory = new RecordingConnectionFactory();
        var manager = TransactionTestHelpers.CreateManager(factory, new RecordingSavepointManager());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.WithTransactionAsync(
                async (scope, token) =>
                {
                    await Task.CompletedTask;
                    throw new InvalidOperationException("boom");
                },
                cancellationToken: CancellationToken.None));

        var connection = factory.Connections.Single();
        Assert.False(connection.LastTransaction?.Committed);
        Assert.True(connection.LastTransaction?.RolledBack);
    }
}
