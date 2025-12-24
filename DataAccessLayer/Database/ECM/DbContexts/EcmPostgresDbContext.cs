using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Database.ECM.DbContexts;

/// <summary>
/// PostgreSQL implementation of the ECM DbContext so EF Core can target Npgsql
/// when <c>AddEcmEntityFrameworkSupport</c> is enabled for that provider.
/// </summary>
public sealed class EcmPostgresDbContext(DbContextOptions<EcmPostgresDbContext> options)
    : EcmDbContextBase(options)
{
}
