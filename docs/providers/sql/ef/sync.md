# SQL Server · EF Core (Sync)

Use EF Core for set-based work while still respecting the layered architecture. Queries that only read data should be `AsNoTracking()` to minimize overhead. Transactions are optional.

## Without Transaction

```csharp
var contextFactory = scope.ServiceProvider.GetRequiredService<IEcmDbContextFactory>();
await using var context = contextFactory.CreateDbContext();

// Read without tracking
var pending = context.Todos
    .AsNoTracking()
    .Where(todo => !todo.IsComplete)
    .ToList();

// Write without an explicit transaction (auto-commit per SaveChanges)
foreach (var todo in pending)
{
    todo.IsComplete = true;
    context.Update(todo);
}

context.SaveChanges();
```

## With `ITransactionScope`

```csharp
var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
var contextFactory = scope.ServiceProvider.GetRequiredService<IEcmDbContextFactory>();
await using var context = contextFactory.CreateDbContext();

using var tx = transactionManager.Begin();
try
{
    context.Database.UseTransaction(tx.Transaction);

    foreach (var todo in pending)
    {
        todo.IsComplete = true;
        context.Update(todo);
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

The same pattern applies to `context.Database.ExecuteSqlRaw` or `DbCommandRequest` integration via `DbContextExtensions.WithAmbientConnection()`: supply the transaction only when you need atomicity.
