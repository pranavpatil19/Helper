# Oracle · ADO.NET (Async)

Async DAL calls integrate with `Oracle.ManagedDataAccess.Core` and support both auto-commit and ambient transactions.

> **Note**  
> Samples assume the relevant services (`IDatabaseHelper`, `ITransactionManager`, `IBulkWriteHelper`) are already injected; remove the DI boilerplate if you store them on fields.

## Without Transaction

```csharp
var request = new DbCommandRequest
{
    CommandText = "pkg_audit.process_batch",
    CommandType = CommandType.StoredProcedure,
    Parameters = DbParameterCollectionBuilder.FromAnonymous(new { p_batch_id = batchId })
};

var result = await helper.ExecuteAsync(request, cancellationToken); // auto-commit
```

## With `ITransactionScope`

```csharp
await using var tx = await transactionManager.BeginAsync(cancellationToken: cancellationToken);

try
{
    await helper.ExecuteAsync(request with { Connection = tx.Connection, Transaction = tx.Transaction }, cancellationToken);
    await helper.ExecuteAsync(otherRequest with { Connection = tx.Connection, Transaction = tx.Transaction }, cancellationToken);
    await tx.CommitAsync(cancellationToken);
}
catch
{
    await tx.RollbackAsync(cancellationToken);
    throw;
}
```

## Bulk helper

```csharp
var operation = new BulkOperation<AuditRow>(new OracleAuditBulkMap());

await bulkHelper.ExecuteAsync(operation, rows, cancellationToken);                // array binding w/out tx

await using var tx = await transactionManager.BeginAsync(cancellationToken: cancellationToken);
await bulkHelper.ExecuteAsync(operation, rows, cancellationToken);                // array binding with tx
await tx.CommitAsync(cancellationToken);
```

### Test coverage

- `tests/DataAccessLayer.Tests/OracleBulkWriterTests`
- `tests/DataAccessLayer.Tests/BulkWriteHelperTests`
