# SQL Server · ADO.NET (Sync)

Use `IDatabaseHelper` directly when you want to execute stored procedures or raw SQL without involving EF. Transactions are optional—when you skip them, the helper opens and closes its own connection per call.

## Without Transaction

```csharp
var helper = scope.ServiceProvider.GetRequiredService<IDatabaseHelper>();

var request = new DbCommandRequest
{
    CommandText = "dbo.ProcessAudit",
    CommandType = CommandType.StoredProcedure,
    Parameters =
    [
        DbParameterCollectionBuilder.Input("BatchId", batchId, DbType.Guid),
        DbParameterCollectionBuilder.Output("RowsTouched", DbType.Int32)
    ]
};

var result = helper.Execute(request);              // auto-commit per call
var rowsTouched = (int?)result.OutputParameters["RowsTouched"];
```

## With `ITransactionScope`

```csharp
var helper = scope.ServiceProvider.GetRequiredService<IDatabaseHelper>();
var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();

using var tx = transactionManager.Begin();
try
{
    helper.Execute(request with { Connection = tx.Connection, Transaction = tx.Transaction });
    helper.Execute(otherRequest with { Connection = tx.Connection, Transaction = tx.Transaction });
    tx.Commit();                                    // rolls all commands back on failure
}
catch
{
    tx.Rollback();
    throw;
}
```

## Bulk helper (optional)

```csharp
var bulkHelper = scope.ServiceProvider.GetRequiredService<IBulkWriteHelper>();
var operation = new BulkOperation<Customer>(new CustomerBulkMap());

// Without transaction
bulkHelper.ExecuteAsync(operation, customers).GetAwaiter().GetResult();

// With transaction
using var tx = transactionManager.Begin();
bulkHelper.ExecuteAsync(operation, customers, CancellationToken.None).GetAwaiter().GetResult();
tx.Commit();
```

Bulk operations honor the same rule: omit the scope for auto-commit, or pass through the ambient scope when you need atomic behavior.
