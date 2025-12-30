using Microsoft.EntityFrameworkCore;
using Shared.Entities;

namespace DataAccessLayer.Database.SCHEDULAR.DbContexts;

/// <summary>
/// Base SCHEDULAR DbContext that exposes shared DbSets and configuration for the destination database.
/// Provider-specific contexts inherit from this type so every provider shares the same model.
/// </summary>
public abstract class SchedularDbContextBase(DbContextOptions options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Auto-discover IEntityTypeConfiguration implementations so each provider-specific context
        // gets the same model configuration without duplicating code whenever EF support is enabled.
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
