using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using DataAccessLayer.EF;
using DataAccessLayer.Common.DbHelper;
using DataAccessLayer.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class CompiledQueryProviderTests
{
    [Fact]
    public void GetOrAdd_CachesDelegate()
    {
        var q1 = CompiledQueryProvider.GetOrAdd<TestDbContext, int>("Count", ctx => ctx.Entities.Count());
        var q2 = CompiledQueryProvider.GetOrAdd<TestDbContext, int>("Count", ctx => ctx.Entities.Count());
        Assert.Same(q1, q2);
    }

    [Fact]
    public async Task SaveChangesWithRetryAsync_UsesResilience()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TestDbContext(options);
        context.Entities.Add(new TestEntity { Name = "A" });

        var strategy = new ResilienceStrategy(new ResilienceOptions(), NullLogger<ResilienceStrategy>.Instance);

        var count = await context.SaveChangesWithRetryAsync(strategy);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DescriptorHandle_ProvidesParameterProfileAndExecutes()
    {
        var descriptor = CompiledQueryDescriptor.Create(
            "Entities.ByName",
            DbParameterCollectionBuilder.Input("p_name", string.Empty));

        var handle = CompiledQueryProvider.GetOrAdd<TestDbContext, string, TestEntity?>(
            descriptor,
            (ctx, name) => ctx.Entities.SingleOrDefault(e => e.Name == name));

        Assert.Single(handle.ParameterProfile);
        Assert.Equal("p_name", handle.ParameterProfile[0].Name);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new TestDbContext(options);
        context.Entities.Add(new TestEntity { Id = 1, Name = "alpha" });
        context.Entities.Add(new TestEntity { Id = 2, Name = "beta" });
        await context.SaveChangesAsync();

        var entity = handle.Execute(context, "alpha");

        Assert.NotNull(entity);
        Assert.Equal("alpha", entity!.Name);
        Assert.NotEqual(0, entity.Id);
    }

    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public DbSet<TestEntity> Entities => Set<TestEntity>();
    }

    private sealed class TestEntity
    {
        public int Id { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
    }
}
