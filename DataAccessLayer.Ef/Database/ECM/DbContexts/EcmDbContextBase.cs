using Microsoft.EntityFrameworkCore;
using Shared.Entities;

namespace DataAccessLayer.Database.ECM.DbContexts;

/// <summary>
/// Base ECM DbContext that exposes shared DbSets and configuration.
/// Provider-specific contexts inherit from this type and are only registered when
/// <c>services.AddEcmEntityFrameworkSupport(...)</c> is called.
/// </summary>
public abstract class EcmDbContextBase(DbContextOptions options) : DbContext(options)
{
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Auto-discover IEntityTypeConfiguration implementations so each provider-specific context
        // gets the same model configuration without duplicating code whenever EF support is enabled.
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
