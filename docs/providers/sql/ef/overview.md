# EF Core Integration – Generic Guidance

## Feature Index
- Shared connection/transaction patterns
- DbContextFactory + compiled query helpers
- Timeout alignment between DAL and EF
- Mapping/tracking considerations for helper + EF interplay

The `Data.EF` folder now includes helpers for compiled queries, shared transactions, and telemetry.

## Patterns
1. **Use Existing Connection**
   ```csharp
   await using var scope = await transactionManager.BeginAsync();
   var context = scopeFactory.CreateDbContext();
   await context.Database.UseTransactionAsync(scope.Transaction, cancellationToken);
   // mix EF and raw helper calls
   await context.SaveChangesAsync(cancellationToken);
   await helper.ExecuteAsync(command with { Connection = scope.Connection, Transaction = scope.Transaction }, cancellationToken);
   await scope.CommitAsync(cancellationToken);
   ```

2. **DbContextFactory + Background Jobs** – inject `IDatabaseHelper` for raw SQL while EF handles change tracking.

3. **Compiled Queries** – use `CompiledQueryProvider` to cache delegates (add `CompiledQueryDescriptor` when you need parameter profiles that line up with `DbParameterDefinition`):
```csharp
private static readonly CompiledQueryHandle<EcmDbContextBase, string, Order?> GetOrderByNumber =
    CompiledQueryProvider.GetOrAdd(
        CompiledQueryDescriptor.Create(
            "Orders.ByNumber",
            DbParameterCollectionBuilder.Input("orderNumber", string.Empty, DbType.String)),
        (EcmDbContextBase ctx, string number) => ctx.Orders.SingleOrDefault(o => o.Number == number));

var order = GetOrderByNumber.Execute(context, orderNumber);
// GetOrderByNumber.ParameterProfile mirrors the same parameter metadata we pass to IDatabaseHelper.
```

## Global Timeout Behavior
- `DatabaseOptions.ConnectionTimeoutSeconds` rewrites the provider connection string before `UseSqlServer/UseNpgsql/UseOracle` runs, so EF contexts inherit the same connect timeout that ADO helpers use.
- `DatabaseOptions.CommandTimeoutSeconds` automatically configures `SqlServerDbContextOptionsBuilder.CommandTimeout(...)` (and equivalent for Npgsql/Oracle). Override per operation via `DbCommandRequest.CommandTimeoutSeconds` or `context.Database.SetCommandTimeout`.
- When you clone a `DbCommandRequest` with `WithAmbientConnection`, the resolved timeout follows the same precedence: explicit request > override options > global options.

## Mapping Considerations
- When materializing into EF entities via `IDatabaseHelper`, decide whether to track: `context.Attach(entity);` or keep DTOs.
- Use mapper delegates to align with EF naming (PascalCase) regardless of underlying column casing.
- Prefer `AsNoTracking()` on EF queries feeding helper pipelines, then explicitly `Attach` only the aggregates you intend to track to avoid ghost state in the change tracker.
- Centralize attachment decisions with `DbContextExtensions.TrackEntities(context, entities, trackEntities: true/false)` so you can opt-in to tracking on a per-query basis after inspecting the results.
- When you need name remapping (snake_case columns → PascalCase properties), either alias columns in SQL (`SELECT field_name AS "Property"`) or pass a column map into the mapper:
  ```csharp
  var columnMap = new Dictionary<string, string>
  {
      ["DisplayName"] = "display_name",
      ["CreatedOn"] = "created_on"
  };
  var mapper = DataMapperFactory.CreateMapper<MyEntity>(columnMap: columnMap);
  ```

## Transactions
- `ITransactionManager` supplies provider-specific savepoints; EF `DbContext.Database.BeginTransaction` is optional if you standardize on the DAL manager.
- To reuse EF-managed transactions, capture `DbContext.Database.CurrentTransaction?.GetDbTransaction()` and assign to `DbCommandRequest.Transaction`.
- `DbContextExtensions.UseAmbientTransaction(scope)` wires the EF context to the DAL `ITransactionScope` so EF and `IDatabaseHelper` share the same connection/transaction.
- `DbContextExtensions.WithAmbientConnection(request, context)` clones a `DbCommandRequest` that executes on the `DbContext` connection.

## Async Safety
- Avoid `Task.Run`/`.Result`; both EF Core and the helper expose async APIs that honor `CancellationToken`.

## Telemetry & Resilience
- `DbContextExtensions.SaveChangesWithRetry()` and `.SaveChangesWithRetryAsync()` run EF persistence inside the same Polly policies used by the DAL.
- `DbCommandLoggingInterceptor` logs EF-generated SQL with execution times; register it via `optionsBuilder.AddInterceptors(new DbCommandLoggingInterceptor(logger))`.
- `BulkExtensions.WriteSqlServerBulkAsync` delegates EF entities to the DAL `SqlServerBulkWriter<T>` so you can flush tracked changes with SqlBulkCopy hot paths.
