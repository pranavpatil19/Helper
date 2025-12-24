# PostgreSQL · EF Core (Async)

Async EF Core calls map directly to Npgsql async operations. Use an ambient transaction only when necessary.

## Without Transaction

```csharp
await using var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

var pending = await context.JobQueue
    .AsNoTracking()
    .Where(job => job.Status == JobStatus.Pending)
    .ToListAsync(cancellationToken);

foreach (var job in pending)
{
    job.Status = JobStatus.Complete;
    context.Update(job);
}

await context.SaveChangesAsync(cancellationToken);    // auto-commit
```

## With `ITransactionScope`

```csharp
var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
await using var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

await using var tx = await transactionManager.BeginAsync(cancellationToken: cancellationToken);
try
{
    context.Database.UseTransaction(tx.Transaction);

    foreach (var job in pending)
    {
        job.Status = JobStatus.Complete;
        context.Update(job);
    }

    await context.SaveChangesAsync(cancellationToken);
    await tx.CommitAsync(cancellationToken);
}
catch
{
    await tx.RollbackAsync(cancellationToken);
    throw;
}
```
