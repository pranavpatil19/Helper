using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Database.SCHEDULAR.DbContexts;

/// <summary>
/// PostgreSQL implementation of the SCHEDULAR DbContext so EF Core can target Npgsql for the destination database.
/// </summary>
public sealed class SchedularPostgresDbContext(DbContextOptions<SchedularPostgresDbContext> options)
    : SchedularDbContextBase(options)
{
}
