using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Database.SCHEDULAR.DbContexts;

/// <summary>
/// SQL Server implementation of the SCHEDULAR DbContext used when the destination runs on SQL Server.
/// </summary>
public sealed class SchedularSqlServerDbContext(DbContextOptions<SchedularSqlServerDbContext> options)
    : SchedularDbContextBase(options)
{
}
