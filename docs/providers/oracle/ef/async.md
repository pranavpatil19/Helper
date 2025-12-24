# Oracle · EF Core (Async)

Async EF work mirrors the sync version, with optional transactions.

## Without Transaction

```csharp
await using var context = scope.ServiceProvider.GetRequiredService<OracleDbContext>();

var pending = await context.BatchJobs
    .AsNoTracking()
    .Where(job => job.State == JobState.Pending)
    .ToListAsync(cancellationToken);

foreach (var job in pending)
{
    job.State = JobState.Complete;
    context.Update(job);
}

await context.SaveChangesAsync(cancellationToken);
```

## With `ITransactionScope`

```csharp
var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
await using var context = scope.ServiceProvider.GetRequiredService<OracleDbContext>();

await using var tx = await transactionManager.BeginAsync(cancellationToken: cancellationToken);
try
{
    context.Database.UseTransaction(tx.Transaction);

    foreach (var job in pending)
    {
        job.State = JobState.Complete;
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
