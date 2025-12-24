# PostgreSQL · EF Core (Sync)

EF Core with Npgsql follows the same guidance: use `AsNoTracking()` for read-only queries, and only wrap work in a transaction if you need atomicity.

## Without Transaction

```csharp
await using var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

var pending = context.JobQueue
    .AsNoTracking()
    .Where(job => job.Status == JobStatus.Pending)
    .ToList();

foreach (var job in pending)
{
    job.Status = JobStatus.Complete;
    context.Update(job);
}

context.SaveChanges();   // auto-commit
```

## With `ITransactionScope`

```csharp
var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
await using var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

using var tx = transactionManager.Begin();
try
{
    context.Database.UseTransaction(tx.Transaction);

    foreach (var job in pending)
    {
        job.Status = JobStatus.Complete;
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
