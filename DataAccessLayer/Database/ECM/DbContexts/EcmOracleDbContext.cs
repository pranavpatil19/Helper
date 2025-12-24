using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Database.ECM.DbContexts;

/// <summary>
/// Oracle implementation of the ECM DbContext. Use this when EF Core needs to
/// run against Oracle (via ODP.NET) and you have opted into EF registrations.
/// </summary>
public sealed class EcmOracleDbContext(DbContextOptions<EcmOracleDbContext> options)
    : EcmDbContextBase(options)
{
}
