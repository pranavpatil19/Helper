# PostgreSQL · ADO.NET (Async)

Async calls use `IDatabaseHelper.Execute*Async`, which internally issues `NpgsqlCommand` instances. A transaction scope is optional.

## Without Transaction

```csharp
var helper = scope.ServiceProvider.GetRequiredService<IDatabaseHelper>();

var request = new DbCommandRequest
{
    CommandText = "public.reconcile_delta",
    CommandType = CommandType.StoredProcedure,
    Parameters = DbParameterCollectionBuilder.FromAnonymous(new { p_batch_id = batchId })
};

var result = await helper.ExecuteAsync(request, cancellationToken);   // COPY/INSERT auto-commit per call
```

## With `ITransactionScope`

```csharp
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

## Bulk helper

```csharp
var bulkHelper = scope.ServiceProvider.GetRequiredService<IBulkWriteHelper>();
var operation = new BulkOperation<Invoice>(new PgInvoiceBulkMap());

await bulkHelper.ExecuteAsync(operation, invoices, cancellationToken);        // COPY w/out scope

await using var tx = await transactionManager.BeginAsync(cancellationToken: cancellationToken);
await bulkHelper.ExecuteAsync(operation, invoices, cancellationToken);        // COPY enlists
await tx.CommitAsync(cancellationToken);
```
