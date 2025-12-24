# PostgreSQL · ADO.NET (Sync)

`IDatabaseHelper` automatically routes commands to Npgsql when `DatabaseOptions.Provider = PostgreSql`. Transactions are optional.

## Without Transaction

```csharp
var helper = scope.ServiceProvider.GetRequiredService<IDatabaseHelper>();

var request = new DbCommandRequest
{
    CommandText = "public.reconcile_delta",
    CommandType = CommandType.StoredProcedure,
    Parameters = DbParameterCollectionBuilder.FromAnonymous(new { p_batch_id = batchId })
};

var result = helper.Execute(request);          // executes via Npgsql; auto-commit per call
```

## With `ITransactionScope`

```csharp
var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
using var tx = transactionManager.Begin();

try
{
    helper.Execute(request with { Connection = tx.Connection, Transaction = tx.Transaction });
    helper.Execute(otherRequest with { Connection = tx.Connection, Transaction = tx.Transaction });
    tx.Commit();
}
catch
{
    tx.Rollback();
    throw;
}
```

## Bulk helper

```csharp
var bulkHelper = scope.ServiceProvider.GetRequiredService<IBulkWriteHelper>();
var operation = new BulkOperation<Invoice>(new PgInvoiceBulkMap());

bulkHelper.ExecuteAsync(operation, invoices).GetAwaiter().GetResult();      // auto-commit COPY

using var tx = transactionManager.Begin();
bulkHelper.ExecuteAsync(operation, invoices).GetAwaiter().GetResult();      // COPY reuses tx
tx.Commit();
```
