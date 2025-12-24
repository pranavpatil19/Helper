# EF Core + SQL Server

## Feature Index
- Shared transaction/connection pattern
- Compiled queries with parameter profiles
- Savepoints, bulk options, and stored procedure integration

## Shared Connection Pattern
```csharp
await using var tx = await transactionManager.BeginAsync();
await dbContext.Database.UseTransactionAsync(tx.Transaction);

// EF operations
var entity = await dbContext.Entities.FindAsync(id);
entity.Status = Status.Completed;
await dbContext.SaveChangesAsync();

// Raw helper call (same transaction)
await helper.ExecuteAsync(command with { Connection = tx.Connection, Transaction = tx.Transaction });

await tx.CommitAsync();
```

## Compiled Queries with Parameter Profiles
Use `CompiledQueryDescriptor` + `CompiledQueryProvider` to cache compiled delegates and keep metadata aligned with your DAL parameter definitions:
```csharp
private static readonly CompiledQueryHandle<EcmDbContextBase, string, Order?> OrderByNumber =
    CompiledQueryProvider.GetOrAdd(
        CompiledQueryDescriptor.Create(
            "Orders.ByNumber",
            DbParameterCollectionBuilder.Input("orderNumber", string.Empty, DbType.String)),
        (EcmDbContextBase ctx, string number) => ctx.Orders.SingleOrDefault(o => o.Number == number));

var result = OrderByNumber.Execute(dbContext, request.Number);
var parameterProfile = OrderByNumber.ParameterProfile; // matches DbParameterDefinition metadata
```
The `ParameterProfile` mirrors the same `DbParameterDefinition` values you would pass to `IDatabaseHelper`, so command logging/telemetry remains consistent between EF and raw ADO paths.

## Savepoints
SQL Server relies on `SAVE TRANSACTION`; call `tx.BeginSavepointAsync("Stage1")` before invoking EF + ADO sequences you might roll back individually.

## Bulk Operations
Until SqlBulkCopy helper lands, prefer:
- `context.BulkInsertAsync` (third-party) OR
- `helper.ExecuteAsync` with table-valued parameters (set `ProviderTypeName = "structured"`).

## OUT Params + EF
When stored procedures return entities plus OUTPUT values, use helper for execution and feed results into EF tracking via `dbContext.Attach(entity)` if needed.
