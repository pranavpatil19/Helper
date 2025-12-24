# Oracle · EF Core (Sync)

Oracle EF Core behaves like other providers: queries should default to `AsNoTracking()` when read-only, and transactions are optional.

## Without Transaction

```csharp
await using var context = scope.ServiceProvider.GetRequiredService<OracleDbContext>();

var pending = context.BatchJobs
    .AsNoTracking()
    .Where(job => job.State == JobState.Pending)
    .ToList();

foreach (var job in pending)
{
    job.State = JobState.Complete;
    context.Update(job);
}

context.SaveChanges();    // auto-commit
```

## With `ITransactionScope`

```csharp
var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
await using var context = scope.ServiceProvider.GetRequiredService<OracleDbContext>();

using var tx = transactionManager.Begin();
try
{
    context.Database.UseTransaction(tx.Transaction);

    foreach (var job in pending)
    {
        job.State = JobState.Complete;
        context.Update(job);
    }

    context.SaveChanges();
    tx.Commit();
}
catch
{
    tx.Rollback();
    throw;
}
```
