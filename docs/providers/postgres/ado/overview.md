# ADO.NET Helper – PostgreSQL Operations

## Feature Index
- Capabilities (savepoints, OUT param emulation, arrays/jsonb, cursor helper)
- Record-returning function example
- Stored procedure emulation with transactions

Provider: `DatabaseProvider.PostgreSql`

## Features
- Commands issued through Npgsql via `IDbConnectionFactory`.
- Savepoints translate to `SAVEPOINT/Rollback/Release Savepoint`.
- OUT parameters are emulated by returning single-row result sets; prefer functions returning composite records.
- Array/JSONB helpers: `PostgresParameterHelper.Array(...)` and `PostgresParameterHelper.Jsonb(...)` return ready-to-use parameters with provider type metadata.
- Dynamic/dictionary projections use `MapperStrategy.Dynamic` / `MapperStrategy.Dictionary` through `DataMapperFactory`.
- REF CURSOR emulation: use `PostgresCursorHelper.BuildMultiCursorRequest(...)` to wrap multiple SELECT statements in a DO block that opens cursors and emits each result set sequentially (consumable via `DbDataReader.NextResult`).

## Function Returning Record
```csharp
var request = new DbCommandRequest
{
    CommandText = "SELECT * FROM accounting.transfer(@source, @target, @amount)",
    Parameters = DbParameterCollectionBuilder.FromAnonymous(new { source = srcId, target = tgtId, amount })
};

var transfers = await db.QueryAsync(request, reader => new TransferResult
{
    SourceBalance = reader.GetDecimal(0),
    TargetBalance = reader.GetDecimal(1)
}, cancellationToken);
```

## Stored Procedure Emulation with OUT Params
```csharp
var request = new DbCommandRequest
{
    CommandText = "CALL payments.process(@payment_id, @amount); SELECT status, reference FROM payments.latest_status();",
    CommandType = CommandType.Text,
    Parameters = DbParameterCollectionBuilder.FromAnonymous(new { payment_id = paymentId, amount })
};

await using var tx = await transactionManager.BeginAsync();
await db.ExecuteAsync(request with { Connection = tx.Connection, Transaction = tx.Transaction });
await tx.CommitAsync();
```

## Handling Lowercase Identifiers
- Configure readers to use column ordinals for best perf.
- For name-based mapping, normalize column names to lowercase inside mapper functions.

## COPY/Bulk
- Use `PostgresBulkWriter<T>` to stream rows via binary `COPY`. Provide `DestinationTable`, column names, and a value selector; optionally pass a custom `COPY ... FROM STDIN` command when targeting partitions or staging tables.

### JSONB + Array Parameter Example
```csharp
var request = new DbCommandRequest
{
    CommandText = "INSERT INTO audit.entries (tags, payload) VALUES (@tags, @payload)",
    Parameters =
    [
        PostgresParameterHelper.Array("tags", new[] { "finance", "approved" }, "_text"),
        PostgresParameterHelper.Jsonb("payload", new { User = userId, Amount = 50 })
    ]
};

await db.ExecuteAsync(request, cancellationToken);
```

## Tips
- Always set `CommandBehavior.SequentialAccess` when streaming large bytea/json payloads via `StreamAsync`.
- Use savepoints to wrap batches: `await tx.BeginSavepointAsync("batch1");`.
- `DatabaseOptions.ConnectionTimeoutSeconds` and `DatabaseOptions.CommandTimeoutSeconds` apply to Npgsql too, so COPY/upload jobs respect the same connect/command thresholds without reconfiguring each request.
- OUT/INOUT parameters: declare them as function outputs, call `DbParameterCollectionBuilder.Output(...)`, and invoke `ExecuteAsync`/`ExecuteScalarAsync`. The helper auto-emulates PostgreSQL OUT parameters by rewriting the stored-proc call into `SELECT * FROM proc(...)` and mapping the first row back into `DbExecutionResult.OutputParameters`, so you get SQL Server–style output dictionaries without extra plumbing.
- List parameters (`DbParameterCollectionBuilder.InputList` or `TreatAsList = true`) are automatically bound as Npgsql arrays, so `WHERE id = ANY(@Ids)` just works without manual looping or string concatenation.
- Need cursor-like behavior? Supply multiple SELECT statements to `PostgresCursorHelper.BuildMultiCursorRequest(...)`, execute it via `LoadDataSetAsync`/`ExecuteReaderAsync`, and consume each result set via `DbDataReader.NextResult` just like Oracle REF CURSOR streams (each cursor is automatically closed when the reader/lease is disposed).

### OUT Parameter Example
```csharp
var request = new DbCommandRequest
{
    CommandText = "accounting.transfer_with_audit",
    CommandType = CommandType.StoredProcedure,
    Parameters =
    [
        DbParameterCollectionBuilder.Input("p_source", sourceId, DbType.Int32),
        DbParameterCollectionBuilder.Input("p_target", targetId, DbType.Int32),
        DbParameterCollectionBuilder.Input("p_amount", amount, DbType.Decimal),
        DbParameterCollectionBuilder.Output("p_status", DbType.String),
        DbParameterCollectionBuilder.Output("p_reference", DbType.String)
    ]
};

var result = await helper.ExecuteAsync(request, cancellationToken);
var status = (string?)result.OutputParameters["p_status"];
var reference = (string?)result.OutputParameters["p_reference"];
```
No custom SQL wrapper is required—the helper runs a synthetic `SELECT` under the covers and hydrates the output dictionary automatically.

## Roadmap Notes
- Upcoming work: JSONB merge helpers, COPY diagnostics, and EF Core-specific extensions (e.g., shared Npgsql connections).
- Structured-type conveniences: keep using `StructuredParameterBuilder.PostgresArray` for now; fluent builders for arrays/records are planned.
