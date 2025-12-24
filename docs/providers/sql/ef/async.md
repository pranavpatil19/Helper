# SQL Server · EF Core (Async)

Async EF operations behave like their sync counterparts: they auto-commit unless you supply an explicit transaction.

## Without Transaction

```csharp
var contextFactory = scope.ServiceProvider.GetRequiredService<IEcmDbContextFactory>();
await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

var pending = await context.Todos
    .AsNoTracking()
    .Where(todo => !todo.IsComplete)
    .ToListAsync(cancellationToken);

foreach (var todo in pending)
{
    todo.IsComplete = true;
    context.Update(todo);
}

await context.SaveChangesAsync(cancellationToken);
```

## With `ITransactionScope`

```csharp
var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
var contextFactory = scope.ServiceProvider.GetRequiredService<IEcmDbContextFactory>();
await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

await using var tx = await transactionManager.BeginAsync(cancellationToken: cancellationToken);
try
{
    context.Database.UseTransaction(tx.Transaction);

    foreach (var todo in pending)
    {
        todo.IsComplete = true;
        context.Update(todo);
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

`context.Database.UseTransaction(tx.Transaction)` enables EF to enlist in the DAL-managed transaction, so every DAL helper (ADO.NET, EF, bulk) participates in the same scope when needed.
