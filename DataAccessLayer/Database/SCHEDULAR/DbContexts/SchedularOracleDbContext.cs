using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Database.SCHEDULAR.DbContexts;

/// <summary>
/// Oracle implementation of the SCHEDULAR DbContext used when destination EF workloads target Oracle.
/// </summary>
public sealed class SchedularOracleDbContext(DbContextOptions<SchedularOracleDbContext> options)
    : SchedularDbContextBase(options)
{
}
