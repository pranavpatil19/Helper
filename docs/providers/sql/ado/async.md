# SQL Server · ADO.NET (Async)

All `IDatabaseHelper` APIs have async counterparts. Transactions remain optional; add an `ITransactionScope` only when multiple statements must succeed or fail together.

## Without Transaction

```csharp
var helper = scope.ServiceProvider.GetRequiredService<IDatabaseHelper>();

var request = new DbCommandRequest
{
    CommandText = "dbo.ProcessAudit",
    CommandType = CommandType.StoredProcedure,
    Parameters = DbParameterCollectionBuilder.FromAnonymous(new { BatchId = batchId })
};

var result = await helper.ExecuteAsync(request, cancellationToken); // auto-commit per call
```

## With `ITransactionScope`

```csharp
var helper = scope.ServiceProvider.GetRequiredService<IDatabaseHelper>();
var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();

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

## Bulk helper (optional)

```csharp
var bulkHelper = scope.ServiceProvider.GetRequiredService<IBulkWriteHelper>();
var operation = new BulkOperation<Customer>(new CustomerBulkMap());

// Without transaction
await bulkHelper.ExecuteAsync(operation, customers, cancellationToken);

// With transaction
await using var tx = await transactionManager.BeginAsync(cancellationToken: cancellationToken);
await bulkHelper.ExecuteAsync(operation, customers, cancellationToken);
await tx.CommitAsync(cancellationToken);
```

Bulk inserts automatically enlist in the ambient scope (if any) or run auto-commit when no scope exists.
