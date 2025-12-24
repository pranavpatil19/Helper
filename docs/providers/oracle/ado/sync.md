# Oracle · ADO.NET (Sync)

The DAL uses `Oracle.ManagedDataAccess.Core` under the hood when `DatabaseOptions.Provider = Oracle`. Transactions remain optional.

> **Note**  
> Samples assume `IDatabaseHelper helper`, `ITransactionManager transactionManager`, and `IBulkWriteHelper bulkHelper` are already injected into your class (constructor or DI container). Replace the variable names with your own.

## Without Transaction

```csharp
var request = new DbCommandRequest
{
    CommandText = "pkg_audit.process_batch",
    CommandType = CommandType.StoredProcedure,
    Parameters = DbParameterCollectionBuilder.FromAnonymous(new { p_batch_id = batchId })
};

var result = helper.Execute(request);   // auto-commit (array binding handled internally if enabled)
```

### Ad-hoc SQL with SqlBuilder

```csharp
var builder = SqlBuilder.Select("o.ORDER_ID", "o.STATUS")
    .From("ORDERS o")
    .Where($"o.STATUS = {status}")
    .OrderBy("o.CREATED_ON DESC");

var request = builder.ToCommandRequest(databaseOptions);
var rows = helper.Query(request,
    mapper: reader => new OrderDto(reader.GetInt32(0), reader.GetString(1)));
```

`SqlSafety` guards every clause (no `--`, `/* */`, or stray `;`), and all interpolated values become DAL parameters so Oracle never sees string-concatenated SQL.

## With `ITransactionScope`

```csharp
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
var operation = new BulkOperation<AuditRow>(new OracleAuditBulkMap());

bulkHelper.ExecuteAsync(operation, rows).GetAwaiter().GetResult(); // array bind per batch

using var tx = transactionManager.Begin();
bulkHelper.ExecuteAsync(operation, rows).GetAwaiter().GetResult();
tx.Commit();
```

### Test coverage

- `tests/DataAccessLayer.Tests/OracleBulkWriterTests` – ensures array binding, batching, and parameter metadata behave as documented.
- `tests/DataAccessLayer.Tests/BulkWriteHelperTests` – verifies Oracle bulk operations succeed both with and without an ambient transaction scope.

### Tips

- Use `DbParameterCollectionBuilder.FromAnonymous(...)` or `.InputList(...)` to emit strongly typed parameters (lists automatically bind as PL/SQL arrays).
- Set `DbCommandRequest.TraceName` so telemetry/logs show friendly names without exposing SQL text.
- Prefer `QueryAsync<T>(request, mapperRequest)` for automatic row mapping (Reflection/IL/source-gen) when you don’t want to hand-write reader code.
