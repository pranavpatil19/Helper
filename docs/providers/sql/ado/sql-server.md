# ADO.NET Helper – SQL Server Operations

## Feature Index
- Provider capabilities (OUTPUT params, savepoints, TVPs, bulk writer)
- Stored procedure patterns
- Transaction/savepoint workflow example

Provider: `DatabaseProvider.SqlServer`

## Capabilities
- Full OUTPUT parameter + RETURN value support.
- Savepoints via `SAVE TRANSACTION` / `ROLLBACK TRANSACTION` handled automatically.
- TVP/structured parameters: use `SqlServerTvpBuilder.ToDataTable(...)` + `SqlServerTvpBuilder.CreateParameter(...)` for strongly-typed payloads.
- High-throughput inserts via `SqlServerBulkWriter<T>` (`SqlBulkCopy` under the hood).

## Stored Procedure Example
```csharp
var result = db.ExecuteStoredProcedure(
    "dbo.UpdateInventory",
    [
        DbParameterCollectionBuilder.Input("ProductId", productId, DbType.Int32),
        DbParameterCollectionBuilder.Input("Delta", delta, DbType.Int32),
        DbParameterCollectionBuilder.Output("Remaining", DbType.Int32)
    ]);
var remaining = (int)result.OutputParameters["Remaining"]!;
```
Prefer constructing the request yourself? Build the base `DbCommandRequest` and finish with `.AsStoredProcedure()` so `CommandType` is set consistently without repeating boilerplate.

## Async Pattern with Savepoint
```csharp
await using var tx = await transactionManager.BeginAsync();
await tx.BeginSavepointAsync("Stage1");

try
{
    await db.ExecuteAsync(commandA with { Connection = tx.Connection, Transaction = tx.Transaction });
    await db.ExecuteAsync(commandB with { Connection = tx.Connection, Transaction = tx.Transaction });
    await tx.CommitAsync();
}
catch
{
    await tx.RollbackToSavepointAsync("Stage1");
    await tx.RollbackAsync();
    throw;
}
```

## Result Shapes
- Use `QueryAsync<Customer>` or `LoadDataTableAsync` for tabular data.
- Use `StreamAsync<Customer>` for large result sets.
- Use `DbExecutionResult.OutputParameters` for RETURN/OUTPUT values.
- Prefer `MapperStrategy.Dictionary` / `MapperStrategy.Dynamic` through `DataMapperFactory` when consuming ad-hoc schemas without defining DTOs.

## Tips
- Set `DbCommandRequest.TraceName` (e.g., `"inventory.update"`) to improve logging/telemetry.
- For high-throughput scenarios, prefer async APIs to prevent thread-pool starvation.
- When mixing EF Core + ADO, call `context.Database.UseTransaction(tx.Transaction)` to share the transaction.
- Tune pooling with `DatabaseOptions.CommandPool` (`EnableCommandPooling`, `EnableParameterPooling`, max retained counts) to balance allocation reduction vs memory.
- Need predictable timeouts? Set `DatabaseOptions.ConnectionTimeoutSeconds` to rewrite the SqlClient connection string and `DatabaseOptions.CommandTimeoutSeconds` (or `DbCommandRequest.CommandTimeoutSeconds`) for per-command limits; both sync and async helper methods honor the same value.
- Need to coordinate changes with other providers? The multi-db coordinator is archived—restore it from `Archive/MultiDbTransactions/` only when necessary.
- Need `IN` clause expansion? Set `TreatAsList = true` (or call `DbParameterCollectionBuilder.InputList`) and reference the parameter once (`WHERE Id IN (@Ids)`); the helper rewrites it to `(@Ids_0,@Ids_1,...)` and binds each value individually.

## Roadmap Notes
- EF-specific bulk shortcuts (e.g., context extensions) and SqlBulkCopy batching options will arrive in later phases.

## Bulk Insert Example
```csharp
var bulkOptions = new SqlServerBulkWriterOptions<OrderSnapshot>
{
    DestinationTable = "dbo.OrderSnapshots",
    ColumnNames = new[] { "Id", "CustomerId", "Amount" },
    ValueSelector = order => new object?[] { order.Id, order.CustomerId, order.Amount }
};

var bulkWriter = new SqlServerBulkWriter<OrderSnapshot>(
    connectionFactory,
    databaseOptions,
    bulkOptions);

await bulkWriter.WriteAsync(orders, cancellationToken);
```

### TVP Builder Example
```csharp
var table = SqlServerTvpBuilder.ToDataTable(
    rows: auditEntries,
    x => x.Id,
    x => x.UserName,
    x => x.ChangedOn);

var tvpParameter = SqlServerTvpBuilder.CreateParameter(
    name: "entries",
    typeName: "dbo.AuditEntryType",
    rows: auditEntries,
    x => x.Id,
    x => x.UserName,
    x => x.ChangedOn);

var request = new DbCommandRequest
{
    CommandText = "dbo.LogAuditEntries",
    CommandType = CommandType.StoredProcedure,
    Parameters =
    [
        tvpParameter
    ]
};

await db.ExecuteAsync(request, cancellationToken);
```
