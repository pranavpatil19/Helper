using System;
using System.Collections.Generic;
using System.Linq;
using DataAccessLayer.EF;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class DbContextExtensionsTests
{
    [Fact]
    public void TrackEntities_AttachesWhenEnabled()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new TestDbContext(options);

        var entities = new List<TestEntity>
        {
            new() { Id = 1, Name = "tracked" }
        };

        context.TrackEntities(entities, trackEntities: true);

        var entry = context.Entry(entities[0]);
        Assert.Equal(EntityState.Unchanged, entry.State);
    }

    [Fact]
    public void TrackEntities_SkipsWhenDisabled()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new TestDbContext(options);

        _ = context.Entities;
        var entity = new TestEntity { Id = 2, Name = "detached" };
        context.TrackEntities(new[] { entity }, trackEntities: false);

        Assert.Empty(context.ChangeTracker.Entries<TestEntity>());
    }

    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public DbSet<TestEntity> Entities => Set<TestEntity>();
    }

    private sealed class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
