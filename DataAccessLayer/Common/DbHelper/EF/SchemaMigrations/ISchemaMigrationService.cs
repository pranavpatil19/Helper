namespace DataAccessLayer.EF;

public interface ISchemaMigrationService
{
    Task ApplyMigrationsAsync(CancellationToken cancellationToken = default);
}
