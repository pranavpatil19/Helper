# Bulk Inserts & Table Mapping

## Feature Index
- Provider support: SQL Server, PostgreSQL, Oracle
- APIs: `BulkWriteHelper`, provider writers (`SqlServerBulkWriter`, `PostgresBulkWriter`, `OracleBulkWriter`)
- Mapping primitives: `BulkMapping`, `BulkColumn`, `BulkOperation`, `BulkOptions`
- Sync + async execution (ADO.NET + EF Core)
- Transaction support (single/multiple tables, rollback)
- EF Core/ LINQ integration
- Package map (ADO vs EF vs LINQ)

The DAL exposes one bulk-writing pipeline for every supported provider (SQL Server, PostgreSQL, Oracle). You can drive it with pure ADO.NET (via `IDatabaseHelper` + `BulkWriteHelper`) or from EF Core (`DbContext` extensions). This document covers the mapping options, sync/async APIs, and where to hook transactions.

## Choosing the right bulk path

| Workload | API surface | Required packages/modules |
| --- | --- | --- |
| Pure ADO.NET (background ETL, table sync) | `IBulkWriteHelper` or provider writers | `DataAccessLayer` (already references `Microsoft.Data.SqlClient`, `Npgsql`, `Oracle.ManagedDataAccess.Core`) with bulk engines left enabled (default) |
| EF Core staying inside `DbContext` | `DataAccessLayer.EF.BulkExtensions` (`WriteSqlServerBulkAsync`, etc.) | `DataAccessLayer` + provider EF package (`Microsoft.EntityFrameworkCore.SqlServer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Oracle.EntityFrameworkCore`) plus `AddEcmEntityFrameworkSupport` |
| LINQ query then ADO bulk insert | Use EF `DbContext` to materialize DTOs, then `IBulkWriteHelper` | Same as ADO row above plus provider EF package if the read/query happens in EF |
| Small batches / transactional row-by-row | `DbContext.SaveChanges()` or `IDatabaseHelper.Execute*` | Provider EF package only (bulk engines optional) |

When you disable bulk engines inside `DalFeatureDefaults`, calls to `IBulkWriteHelper` and the EF bulk extensions throw explaining the feature is disabled. Flip the flag back to true—or re-add the `Common/DbHelper/Bulk` folder—to bring the engines online again.

## Common building blocks

1. **Row mapping** – Map your CLR row type to table columns via `BulkMapping`/`BulkColumn`:
   ```csharp
   var mapping = BulkMapping
       .ForTable("dbo.Customers")
       .Columns(
           BulkColumn.Create("CustomerId", row => row.Id),
           BulkColumn.Create("Name", row => row.Name),
           BulkColumn.Create("CreatedUtc", row => row.CreatedUtc));
   ```

2. **Bulk operation** – Wrap the mapping + options:
   ```csharp
   var operation = new BulkOperation<CustomerRow>(mapping, new BulkOptions
   {
       BatchSize = 1000,
       KeepIdentity = true,
       OverrideOptions = sourceEndpointOptions // optional DatabaseOptions override
   });
   ```

3. **Execute (async or sync)** – Use `BulkWriteHelper` (provider agnostic) or call a provider-specific writer directly.
   ```csharp
   await bulkWriteHelper.ExecuteAsync(operation, rows, cancellationToken);
   ```

The helper automatically chooses the correct `IBulkEngine` (SqlBulkCopy, PostgreSQL COPY, Oracle array binding) that was registered during `AddDataAccessLayer`. All engines accept an ambient `DbTransaction` via `ITransactionManager`, so you can bulk-insert multiple tables inside a single commit/rollback window.

### Direct writers vs helper

- `BulkWriteHelper` → easiest path; handles provider dispatch, batching, and telemetry for you.
- `SqlServerBulkWriter<T>`, `PostgresBulkWriter<T>`, `OracleBulkWriter<T>` → lower-level writers if you need to customize the connection/transaction yourself. Each exposes `Write(rows)` and `WriteAsync(rows, cancellationToken)`.

## SQL Server

### ADO.NET path

```csharp
var mapping = ...;
var operation = new BulkOperation<CustomerRow>(mapping, new BulkOptions { UseTableLock = true });
await bulkWriteHelper.ExecuteAsync(operation, rows, cancellationToken);
```

Under the hood the `SqlServerBulkEngine` uses `SqlBulkCopy` (`SqlBulkCopyClientFactory`). The DAL keeps the same connection and transaction from `ITransactionManager`, so you can bulk multiple tables sequentially and call `scope.CommitAsync()` at the end. If any table fails, catch the exception and `scope.RollbackAsync()` to undo every bulk insert.

Need full control? Instantiate `SqlServerBulkWriter<T>` directly and call `Write`/`WriteAsync` after configuring `SqlServerBulkWriterOptions<T>` (destination table, column list, value selector, optional override connection string).

### EF Core path

`DataAccessLayer.EF.BulkExtensions` exposes `WriteSqlServerBulkAsync`:

```csharp
await dbContext.WriteSqlServerBulkAsync(
    rows,
    new SqlServerBulkWriterOptions<RowDto>
    {
        DestinationTable = "dbo.Customers",
        ColumnNames = new[] { "CustomerId", "Name" },
        ValueSelector = row => new object?[] { row.Id, row.Name }
    },
    services,
    cancellationToken);
```

**LINQ example with BulkOptions**

```csharp
// Query records with EF / LINQ first
var pending = await dbContext.Orders
    .Where(order => order.Status == OrderStatus.Pending)
    .Select(order => new OrderRow
    {
        Id = order.Id,
        Amount = order.Amount,
        CreatedUtc = order.CreatedUtc
    })
    .ToListAsync(cancellationToken).ConfigureAwait(false);

// Reuse DAL bulk mapping/options
var mapping = BulkMapping
    .ForTable("reporting.PendingOrders")
    .Columns(
        BulkColumn.Create("OrderId", row => row.Id),
        BulkColumn.Create("Amount", row => row.Amount),
        BulkColumn.Create("CreatedUtc", row => row.CreatedUtc));

var operation = new BulkOperation<OrderRow>(mapping, new BulkOptions
{
    BatchSize = 2_000,
    OverrideOptions = destinationEndpoint.Database
});

await bulkWriteHelper.ExecuteAsync(operation, pending, cancellationToken).ConfigureAwait(false);
```

Here EF Core pulls the data via LINQ, and the DAL bulk helper handles the actual insert. `BulkOptions.BatchSize` throttles the writer for large tables, and `OverrideOptions` can point at the destination endpoint (sql/sql+LINQ, Oracle, PostgreSQL, etc.).

EF uses the DAL services registered in DI (`IDbConnectionFactory`, `ISqlBulkCopyClientFactory`, `DatabaseOptions`) so you retain the same telemetry, batching, and connection lifecycle. Because the writer implements both `Write` and `WriteAsync`, you can choose the mode that best fits your workload.

## PostgreSQL

`PostgresBulkEngine` uses COPY (binary) through `PostgresBulkWriter`. Usage mirrors SQL Server:

```csharp
var mapping = ...; // ColumnNames, CopyCommand, ValueSelector
var operation = new BulkOperation<OrderRow>(mapping, new BulkOptions());
await bulkWriteHelper.ExecuteAsync(operation, rows, cancellationToken);
```

Advanced customization: instantiate `PostgresBulkWriter<T>` with either a shared `DbConnection` (for ambient transactions) or let it open connections via `IDbConnectionFactory`. Provide column names + `ValueSelector` (ensures value count matches column count). The writer exposes `Write` and `WriteAsync` so you can choose synchronous or asynchronous streaming to COPY.

## Oracle

`OracleBulkEngine` array-binds your row batch via `OracleBulkWriter<T>`:

```csharp
var mapping = ...;
var options = new OracleBulkWriterOptions<InvoiceRow>
{
    CommandText = "INSERT INTO Invoices (...) VALUES (:p0, :p1, :p2)",
    ParameterNames = new[] { ":p0", ":p1", ":p2" },
    BatchSize = 256,
    ValueSelector = row => new object?[] { row.Id, row.Amount, row.CreatedUtc }
};
await oracleBulkWriter.WriteAsync(rows, cancellationToken);
```

As with other providers, the writer honors the current `DbTransaction` when you pass a shared connection. The `ValueSelector` length must match the parameter count; otherwise `BulkOperationException` is thrown.

## Transactions & rollbacks

- Start a transaction via `ITransactionManager` / `TransactionScope`. Every bulk insert within that scope uses the same connection + `DbTransaction`.
- If any table fails, catch the exception and call `scope.RollbackAsync()` (or dispose without committing). This reverts all inserts, whether they used `BulkWriteHelper` or direct writers.
- Once every table is imported successfully, call `scope.CommitAsync()` to persist the batch.

## Sync vs async

- `BulkWriteHelper` currently exposes `ExecuteAsync`. If you need synchronous bulk writes, call the provider writer’s `Write(rows)` directly from your orchestration.
- All writers (`SqlServerBulkWriter`, `PostgresBulkWriter`, `OracleBulkWriter`) implement both `Write` (sync) and `WriteAsync` (async) so you can stay synchronous in worker threads or go fully async for large imports.

## Tests & verification

The solution already exercises bulk paths through `BulkWriteHelperTests`, `SqlServerBulkWriterTests`, `OracleBulkWriterTests`, etc. Running `dotnet test` executes those suites as well as the new migration-runner tests and confirms that bulk mapping works across providers.
