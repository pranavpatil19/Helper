# EF Core + PostgreSQL

## Feature Index
- Transaction sharing pattern
- Array/JSON parameter alignment
- Savepoints/retry flow
- COPY integration pointers
- Naming convention tips

## Transaction Sharing
```csharp
await using var tx = await transactionManager.BeginAsync(options: postgresOptions);
await dbContext.Database.UseTransactionAsync(tx.Transaction);

await dbContext.SaveChangesAsync();
await helper.ExecuteAsync(command with { Connection = tx.Connection, Transaction = tx.Transaction });
await tx.CommitAsync();
```

## Array/JSON Columns
- EF maps `List<string>` → `text[]`, `JsonDocument` → `jsonb`.
- When supplementing with helper queries, ensure parameter `ProviderTypeName` matches EF column type (`"jsonb"`, `"uuid[]"`).

## Savepoints for Retry
PostgreSQL supports `SAVEPOINT/ROLLBACK/RELEASE`. Wrap retryable segments:
```csharp
await tx.BeginSavepointAsync("retry1");
try
{
    await helper.ExecuteAsync(...);
}
catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
{
    await tx.RollbackToSavepointAsync("retry1");
    // regenerate SQL or re-run EF logic
}
```

## COPY + EF
Phase C bulk helpers will orchestrate COPY via `IBulkWriter`. For now, open `NpgsqlBinaryImporter` against `tx.Connection` to keep EF and COPY in the same transaction.

## Naming Conventions
- EF typically maps properties to quoted identifiers; when using helper mappers, normalize names to lowercase to match Npgsql’s default behavior.
