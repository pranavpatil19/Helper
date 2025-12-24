using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Database.ECM.DbContexts;

/// <summary>
/// SQL Server implementation of the ECM DbContext. Registered through
/// <c>AddEcmEntityFrameworkSupport</c> when the data access layer needs EF
/// capabilities (migrations, repositories) against SQL Server.
/// </summary>
public sealed class EcmSqlServerDbContext(DbContextOptions<EcmSqlServerDbContext> options)
    : EcmDbContextBase(options)
{
}
