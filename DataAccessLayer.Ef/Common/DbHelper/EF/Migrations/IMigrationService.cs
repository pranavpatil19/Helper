namespace DataAccessLayer.EF;

public interface IMigrationService
{
    Task ApplyMigrationsAsync(CancellationToken cancellationToken = default);
}
