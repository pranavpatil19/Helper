# EF Core + Oracle

## Feature Index
- Transaction/ambient connection coordination
- REF CURSOR materialization + EF tracking
- Savepoints, array binding, JSON/LOB handling, future enhancements

## Transaction Coordination
```csharp
await using var tx = await transactionManager.BeginAsync(options: oracleOptions);
await dbContext.Database.UseTransactionAsync(tx.Transaction, cancellationToken);

await dbContext.SaveChangesAsync(cancellationToken);
await helper.ExecuteAsync(procCommand with { Connection = tx.Connection, Transaction = tx.Transaction }, cancellationToken);
await tx.CommitAsync(cancellationToken);
```

## REF CURSOR with EF Entities
- Execute stored procedures via helper to obtain REF CURSOR readers.
- Materialize DTOs and attach to EF if tracking is required:
```csharp
var histories = await helper.QueryAsync(historyRequest, r => new HistoryEntity { ... }, cancellationToken);
foreach (var item in histories.Data)
{
    dbContext.Attach(item);
}
```

## Savepoints for Complex Workflows
Oracle supports `SAVEPOINT`/`ROLLBACK TO SAVEPOINT`. Use `tx.BeginSavepointAsync("step")` before mixing EF updates with raw PL/SQL operations, then release/commit when successful.

## Array Binding + EF
When EF change tracking becomes expensive for bulk adjustments, fall back to helper array bindings. Ensure EF’s context is aware of resulting changes (e.g., reload entities, or operate on tables not tracked by EF).

## JSON/LOB Columns
- EF maps `string`/`JsonDocument` to CLOB/JSON columns; when issuing raw SQL, prefer streaming via `StreamAsync` and `CommandBehavior.SequentialAccess` to avoid buffering.

## Future Enhancements
- EF interceptors for Oracle telemetry and retry logic.
- Migration helpers for Oracle-specific features (identity columns, sequences, triggers).
- Bulk helpers bridging EF change tracker to Oracle array DML/pipelined functions.
