## DatabaseHelper Folder Playbook (SQL Server 2005 / PostgreSQL 8 / Oracle 9)

This document mirrors the structure inside `DataAccessLayer/Common/DbHelper/DatabaseHelper` and captures every public entry point, its default validation/telemetry behavior, and provider-specific guidance. Treat it as the single reference when you need to understand how the helper behaves per class, how metadata is validated, and how to author requests that work unchanged across SQL Server 2005, PostgreSQL 8.x, and Oracle 9.

### Why this folder matters
- Centralizes reusable ADO.NET logic (connection pooling, retries, telemetry) behind `IDatabaseHelper`.
- Enforces deterministic parameter metadata (size, precision/scale, provider types) through `DbParameter` helpers + validators.
- Documents the private helpers inside `DatabaseHelper.Core.cs` (`ExecuteScalarLike`, Postgres/Oracle adapters) so it’s obvious why they exist.

---

## Quick reference (copy/paste)

### DML/DDL + OUT params (`ExecuteAsync`)

```csharp
var dmlRequest = new DbCommandRequest
{
    CommandText = "dbo.User_UpdateLogin",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("UserId", userId, DbType.Guid),
        DbParameter.Input("LastLoginUtc", DateTimeOffset.UtcNow, DbType.DateTimeOffset),
        DbParameter.Output("RowsTouched", DbType.Int32),
        DbParameter.ReturnValue(DbType.Int32)
    },
    CommandTimeoutSeconds = 30,
    TraceName = "user.update-login",
    Validate = true
};
var result = await helper.ExecuteAsync(dmlRequest);
```

### Scalar result (`ExecuteScalarAsync`)

```csharp
var scalarRequest = new DbCommandRequest
{
    CommandText = "SELECT COUNT(*) FROM dbo.Users WHERE IsActive = @IsActive",
    CommandType = CommandType.Text,
    Parameters = new[] { DbParameter.Input("IsActive", true, DbType.Boolean) },
    CommandTimeoutSeconds = 15,
    TraceName = "users.count-active"
};
var total = await helper.ExecuteScalarAsync(scalarRequest);
```

### Materialize POCO list (`QueryAsync<T>`)

```csharp
var queryRequest = new DbCommandRequest
{
    CommandText = "SELECT Id, Email FROM dbo.Users WHERE IsActive = @IsActive",
    CommandType = CommandType.Text,
    Parameters = new[] { DbParameter.Input("IsActive", true, DbType.Boolean) },
    CommandTimeoutSeconds = 30,
    TraceName = "users.query-active"
};
var users = await helper.QueryAsync(queryRequest, r => new User(r.GetGuid(0), r.GetString(1)));
```

### Multi-table buffer (`LoadDataSetAsync`)

```csharp
var multiRequest = new DbCommandRequest
{
    CommandText = "dbo.User_GetAndOrders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[] { DbParameter.Input("UserId", userId, DbType.Guid) },
    CommandTimeoutSeconds = 60,
    TraceName = "user.orders-data-set"
};
var dataSet = await helper.LoadDataSetAsync(multiRequest);
```

### Stream large rows (`StreamColumnAsync`)

```csharp
var blobRequest = new DbCommandRequest
{
    CommandText = "SELECT Document FROM dbo.UserDocuments WHERE Id = @Id",
    CommandType = CommandType.Text,
    Parameters = new[] { DbParameter.Input("Id", documentId, DbType.Guid) },
    CommandBehavior = CommandBehavior.SequentialAccess,
    CommandTimeoutSeconds = 180,
    TraceName = "files.stream-document"
};
await using var destination = File.Create(path);
var bytes = await helper.StreamColumnAsync(blobRequest, ordinal: 0, destination);
```

### Stored procedure shortcut (`QueryStoredProcedureAsync`)

```csharp
var rows = await helper.QueryStoredProcedureAsync<User>(
    "dbo.User_Search",
    mapperRequest: new RowMapperRequest { Strategy = MapperStrategy.Reflection },
    parameters: new[] { DbParameter.Input("Term", term, DbType.String, size: 64) },
    cancellationToken: token);
```

### Oracle REF CURSOR (`ExecuteRefCursorAsync`)

```csharp
var oracleRequest = new DbCommandRequest
{
    CommandText = "pkg_user.get_profile",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("p_user_id", userId, DbType.Guid),
        DbParameter.Output("p_profile_cursor", DbType.Object, providerTypeName: "RefCursor")
    },
    CommandTimeoutSeconds = 45,
    TraceName = "oracle.profile"
};
await using var scope = await helper.ExecuteRefCursorAsync(oracleRequest, "p_profile_cursor");
```

### PostgreSQL multi-result (`PostgresCursorHelper` + `LoadDataSetAsync`)

```csharp
var cursorPlan = PostgresCursorHelper.BuildMultiCursorRequest(
    new[]
    {
        "SELECT id, email FROM app.users WHERE id = @user_id",
        "SELECT id, total FROM app.orders WHERE user_id = @user_id"
    },
    new[] { DbParameter.Input("user_id", userId, DbType.Guid) },
    traceName: "pg.user-orders");

var cursorRequest = new DbCommandRequest
{
    CommandText = cursorPlan.CommandText,
    CommandType = cursorPlan.CommandType,
    Parameters = cursorPlan.Parameters,
    TraceName = cursorPlan.TraceName,
    CommandTimeoutSeconds = 60
};

var pgDataSet = await helper.LoadDataSetAsync(cursorRequest);
```

---

## 1. Bootstrapping & Options

```csharp
var options = new DatabaseOptions
{
    Provider = DatabaseProvider.SqlServer,
    ConnectionString = configuration.GetConnectionString("Database"),
    WrapProviderExceptions = true, // default: wrap provider errors in DataException
    Resilience = { CommandRetryCount = 3 },
    ParameterBinding = { TrimStrings = true }
};

await using var dal = DalHelperFactory.Create(options);
var helper = dal.DatabaseHelper;
```

Key facts:

| Setting | Where | Notes |
|---------|-------|-------|
| `WrapProviderExceptions` | `Shared.Configuration.DatabaseOptions` or per-request `DbCommandRequest.OverrideOptions` | `true` (default) surfaces a single `DataException` regardless of provider. `false` lets `SqlException`/`NpgsqlException`/`OracleException` bubble. |
| `DbCommandRequest.Validate` | `DbCommandRequest` | Defaults to `true`. Runs FluentValidation (`DbCommandRequestValidator` + `DbParameterDefinitionValidator`) plus normalization. Set `false` only when inputs are already sanitized. |
| `TraceName` | `DbCommandRequest.TraceName` | Propagates to `IDataAccessTelemetry` and `ActivitySource("Helper.DataAccessLayer.Database")`. |
| `CommandBehavior` | `DbCommandRequest.CommandBehavior` | Use `SequentialAccess` for streaming APIs; defaults to provider default for buffered calls. |

---

## 2. DbCommandRequest blueprint

Every example in this doc builds a `DbCommandRequest`. Here is the canonical template with the most common knobs populated:

```csharp
var request = new DbCommandRequest
{
    CommandText = "dbo.User_UpdateLogin",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("UserId", userId, DbType.Guid),
        DbParameter.Input("LastLoginUtc", DateTimeOffset.UtcNow, DbType.DateTimeOffset, isNullable: false),
        DbParameter.Output("RowsTouched", DbType.Int32)
    },
    CommandTimeoutSeconds = 45,
    CommandBehavior = CommandBehavior.Default,
    TraceName = "user.update-login",
    PrepareCommand = false,
    Validate = true,
    OverrideOptions = optionsOverride,   // optional
    Connection = externalConnection,    // optional
    Transaction = externalTransaction,  // optional
    CloseConnection = false,
    CommandType = CommandType.StoredProcedure
};
```

**Field primer**

| Property | Why it matters |
|----------|----------------|
| `CommandText` | Procedure name or SQL text (no provider prefixes needed). |
| `CommandType` | `CommandType.StoredProcedure` or `CommandType.Text`. |
| `Parameters` | Always supply explicit metadata via `DbParameter.*` helpers. |
| `CommandTimeoutSeconds` | Overrides provider default for long-running commands. |
| `CommandBehavior` | Use `SequentialAccess` for streaming; otherwise leave `Default`. |
| `TraceName` | Shows up in telemetry/logging. |
| `Validate` | Keep `true` to run FluentValidation + normalization. |
| `OverrideOptions` | Provide when you must change provider/connection string at call time. |
| `Connection`/`Transaction` | Set when you bypass the ambient scope (rare). |
| `CloseConnection` | Set `true` when you pass your own connection and want helper to close it. |

All later snippets assume this baseline; when we omit a property it simply uses the defaults above.

---

## 3. Parameter Definition Reference

### 3.1 Metadata surface (`DbParameterDefinition`)

| Property | Purpose | Validation / Notes |
|----------|---------|--------------------|
| `Name` | Logical name without provider prefix. | Required; validators reject empty names. |
| `DbType` | Explicit type so binding is deterministic. | Supply for every parameter (input/output/return). |
| `Direction` | Input, Output, InputOutput, ReturnValue. | Helper chooses correct provider API based on direction. |
| `Size` | Length for `DbType.String`/`Binary`. | Must be > 0 when supplied; enforced by binder. |
| `Precision`/`Scale` | Decimal metadata. | Precision 1–38, `Scale <= Precision`. Normalization rounds per `InputNormalizationOptions`. |
| `IsNullable` | Whether nulls are accepted. | Combine with `defaultValue` when procedure has defaults. |
| `DefaultValue` | Substitutes `null`/default struct values. | Applied before validation. |
| `ProviderTypeName` | Native type alias (`jsonb`, `_uuid`, `RefCursor`). | Sanitized unless `ParameterBinding.AllowUnsafeProviderTypeNames = true`. |
| `TreatAsList`/`Values` | Marks arrays/TVPs. | Validators ensure `Values` is present when `TreatAsList = true`. |
| `ValueConverter` | Runs before binding. | Useful when providers need a bespoke representation (e.g., `bool` to `"Y"/"N"`). |

### 3.2 Cross-provider `DbType` mapping

| `DbType` | SQL Server 2005 | PostgreSQL 8.x | Oracle 9 |
|----------|-----------------|----------------|----------|
| `Guid` | `uniqueidentifier` | `uuid` | `RAW(16)` (helper converts) |
| `Boolean` | `bit` | `boolean` | `NUMBER(1)` (helper coerces) |
| `DateTimeOffset` | `datetimeoffset` | `timestamptz` (stored as UTC) | `TIMESTAMP WITH TIME ZONE` (converted to UTC `DateTime`) |
| `Decimal` | `decimal(p,s)` | `numeric(p,s)` | `NUMBER(p,s)` |
| `String` | `nvarchar(size)`/`nvarchar(max)` | `varchar(size)`/`text` (size stored for validation) | `NVARCHAR2(size)` |
| `Binary` | `varbinary(size)`/`varbinary(max)` | `bytea` | `RAW(size)` / `BLOB` |
| `Object` | table-valued parameters / XML | arrays (`_uuid`, `_int4`) or JSONB | REF CURSOR (`providerTypeName: "RefCursor"`), array binding |

### 3.3 Parameter helper cheatsheet

```csharp
var parameters = new[]
{
    // Shared metadata
    DbParameter.Input("UserId", userId, DbType.Guid),
    DbParameter.Input("DisplayName", payload.DisplayName, DbType.String, size: 64, isNullable: true),
    DbParameter.Input("IsActive", payload.IsActive, DbType.Boolean),
    DbParameter.Input("CreditLimit", payload.CreditLimit, DbType.Decimal, precision: 10, scale: 2),

    // List/array variants
    DbParameter.InputList("RoleIds", payload.RoleIds, DbType.Guid, providerTypeName: "_uuid"),     // PostgreSQL array
    StructuredParameterBuilder.SqlServerTableValuedParameter("UserIds", payload.TvpValue, "dbo.UserIdList"),
    StructuredParameterBuilder.OracleArray("Tags", payload.Tags, DbType.String, size: 32),

    // Outputs
    DbParameter.Output("RowsTouched", DbType.Int32),
    DbParameter.InputOutput("RetryCount", payload.RetryCount, DbType.Int32),
    DbParameter.ReturnValue(DbType.Int32),

    // Oracle REF CURSOR
    OracleParameterHelper.RefCursor("p_orders")
};
```

Remember: if a stored procedure parameter is optional, you can either omit it entirely or pass `DbParameter.Input("OptionalFlag", null, DbType.Boolean, isNullable: true)` so validation knows null is intentional.

---

## 4. Class-by-class feature guide

### 4.1 Core infrastructure (`DatabaseHelper.Core.cs`)

| Helper | Reason it exists | Provider notes |
|--------|------------------|----------------|
| `ValidateRequest` | Runs guard clauses + FluentValidation before every command. | Honors `request.Validate`. |
| `ExecuteWithRequestActivity*` | Wraps execution in telemetry (`ActivitySource`) and logging scopes. | Adds tags such as command type, row count, stream bytes. |
| `ExecuteWithResilience*` | Applies `DatabaseOptions.Resilience` Polly policies (retries/timeouts). | Transparent to callers; synchronous and async variants. |
| `ExecuteScalarLike*` | Shared pipeline that captures rows affected + scalar + outputs for `Execute*` and `ExecuteScalar*`. Keeps duplication minimal, which is why it lives in the core partial. |
| `TryExecutePostgresOutParameters*` | Rewrites stored procedures with OUT params into a SELECT plan so PostgreSQL (pre v11) can return outputs + scalar in a single row. | Automatically triggered when a command contains OUT/Return parameters and provider is PostgreSQL. |
| `ExecuteRefCursorCore*` | Executes Oracle packages that expose REF CURSOR outputs and returns a `DbReaderScope`. | Throws `ProviderNotSupportedException` if provider is not Oracle. |
| `WrapException` | Applies `WrapProviderExceptions`. | Respects per-request overrides. |

### 4.2 Commands (`DatabaseHelper.Commands.cs`)

| Method | Returns | Use when | Provider nuances |
|--------|---------|----------|------------------|
| `ExecuteAsync/Execute` | `DbExecutionResult` (rows affected + outputs). | DML/DDL or stored procedures that only return OUT params. | PostgreSQL OUT params rewritten to SELECT; Oracle async wraps sync provider calls. |
| `ExecuteScalarAsync/ExecuteScalar` | Scalar plus outputs. | Need a single value (identity, computed aggregate). | Same SELECT rewrite for PostgreSQL. |

**SQL Server example (`dbo.User_UpdateLogin`)**

```csharp
var request = new DbCommandRequest
{
    CommandText = "dbo.User_UpdateLogin",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("UserId", userId, DbType.Guid),
        DbParameter.Input("LastLoginUtc", DateTimeOffset.UtcNow, DbType.DateTimeOffset),
        DbParameter.Output("RowsTouched", DbType.Int32),
        DbParameter.ReturnValue(DbType.Int32)
    },
    CommandTimeoutSeconds = 30,
    TraceName = "user.update-login",
    Validate = true
};

var result = await helper.ExecuteAsync(request);
Console.WriteLine($"Rows: {result.RowsAffected}, OUT: {result.OutputParameters["RowsTouched"]}");
```

**PostgreSQL example (`app.update_user_last_login`)**

```csharp
var request = new DbCommandRequest
{
    CommandText = "call app.update_user_last_login(@user_id, @last_login, @rows_out OUT)",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("user_id", userId, DbType.Guid),
        DbParameter.Input("last_login", DateTime.UtcNow, DbType.DateTime),
        DbParameter.Output("rows_out", DbType.Int32)
    },
    CommandTimeoutSeconds = 45,
    TraceName = "user.update-login.pg"
};

var result = await helper.ExecuteAsync(request); // internally rewritten to SELECT ...
```

**Oracle example (`pkg_user.update_status`)**

```csharp
var request = new DbCommandRequest
{
    CommandText = "pkg_user.update_status",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("p_user_id", userId, DbType.Guid),
        DbParameter.Input("p_status", payload.Status, DbType.String, size: 16),
        DbParameter.Output("p_rows", DbType.Int32)
    },
    CommandTimeoutSeconds = 30,
    TraceName = "user.update-status.oracle"
};

var result = await helper.ExecuteAsync(request); // async wraps sync provider calls
```

### 4.3 Queries & DataSets (`DatabaseHelper.Queries.cs`)

| Method | Description | Notes |
|--------|-------------|-------|
| `QueryAsync/Query` | Executes reader and maps every row via delegate/mapper. | Fully buffered list; great for POCO materialization. |
| `LoadDataTableAsync/LoadDataTable` | Populates a single `DataTable`. | Honors `CommandBehavior`. |
| `LoadDataSetAsync/LoadDataSet` | Buffers every result set into a `DataSet`. | Works with SQL Server multi-result, Postgres cursors, Oracle REF CURSORs (via helper). |

Examples:

- **SQL Server (`dbo.User_GetProfile`)**

```csharp
var query = new DbCommandRequest
{
    CommandText = "dbo.User_GetProfile",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[] { DbParameter.Input("UserId", userId, DbType.Guid) },
    CommandTimeoutSeconds = 30,
    TraceName = "user.get-profile"
};

var profile = await helper.QueryAsync(query, reader => new UserDto
{
    Id = reader.GetGuid(0),
    Email = reader.GetString(1)
});
```

- **PostgreSQL multi-result via cursors**

```csharp
var cursorRequest = PostgresCursorHelper.BuildMultiCursorRequest(
    new[]
    {
        "SELECT * FROM app.users WHERE id = @user_id",
        "SELECT * FROM app.orders WHERE user_id = @user_id"
    },
    new[] { DbParameter.Input("user_id", userId, DbType.Guid) },
    traceName: "proc.user-and-orders");

var userOrders = await helper.LoadDataSetAsync(cursorRequest);

// Need a custom timeout/behavior? Build a DbCommandRequest manually using the blueprint section above.
```

- **Oracle REF CURSOR buffered into a table**

```csharp
var refCursorRequest = new DbCommandRequest
{
    CommandText = "pkg_user.get_orders",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("p_user_id", userId, DbType.Guid),
        OracleParameterHelper.RefCursor("p_orders")
    }
};

await using var scope = await helper.ExecuteRefCursorAsync(refCursorRequest, "p_orders");
var table = new DataTable();
table.Load(scope.Reader);
```

### 4.4 Streaming (`DatabaseHelper.Streaming.cs`)

| Method | Purpose | Notes |
|--------|---------|-------|
| `StreamAsync<T>` | `IAsyncEnumerable<T>` that reads rows lazily. | Use when result sets are huge; mapper runs per row. |
| `StreamColumnAsync/StreamColumn` | Streams a single binary column into `Stream`. | Automatically sets sequential access. |
| `StreamTextAsync/StreamText` | Streams a text column. | Useful for CLOB/NVARCHAR(MAX)/TEXT payloads. |

Example (SQL Server BLOB):

```csharp
var download = new DbCommandRequest
{
    CommandText = "SELECT Document FROM dbo.UserDocuments WHERE Id = @Id",
    CommandType = CommandType.Text,
    Parameters = new[] { DbParameter.Input("Id", documentId, DbType.Guid) },
    CommandBehavior = CommandBehavior.SequentialAccess,
    CommandTimeoutSeconds = 120,
    TraceName = "files.stream-document"
};

await using var file = File.Create(tempPath);
var bytes = await helper.StreamColumnAsync(download, ordinal: 0, file);
```

Example (PostgreSQL row streaming):

```csharp
var streamRequest = new DbCommandRequest
{
    CommandText = "SELECT id, total FROM app.orders WHERE user_id = @UserId ORDER BY id",
    CommandType = CommandType.Text,
    Parameters = new[]
    {
        DbParameter.Input("UserId", userId, DbType.Guid)
    },
    CommandBehavior = CommandBehavior.SequentialAccess,
    CommandTimeoutSeconds = 120,
    TraceName = "orders.stream"
};

await foreach (var row in helper.StreamAsync(
                   streamRequest,
                   reader => new { Id = reader.GetGuid(0), Total = reader.GetDecimal(1) },
                   cancellationToken))
{
    // process row
}
```

### 4.5 Stored procedure conveniences (`DatabaseHelper.StoredProcedures.cs`)

| Method | Wraps | When to use |
|--------|-------|-------------|
| `ExecuteStoredProcedure*` | `Execute*` | When you only have the procedure name + parameters. |
| `QueryStoredProcedure*` | `Query*` | Materialize results with delegate/mapper. |
| `ExecuteStoredProcedureReader*` | `ExecuteReader*` | Need raw `DbDataReader` / manual control. |

Example (SQL Server multi-result):

```csharp
await using var scope = await helper.ExecuteStoredProcedureReaderAsync(
    "dbo.User_GetAndOrders",
    new[] { DbParameter.Input("UserId", userId, DbType.Guid) });

await using var reader = scope.Reader;
while (await reader.ReadAsync()) { /* user */ }
await reader.NextResultAsync();
while (await reader.ReadAsync()) { /* orders */ }
```

### 4.6 Reader scopes (`DatabaseHelper.ReaderScopes.cs`)

`ExecuteReaderAsync`/`ExecuteReader` ⇒ `DbReaderScope` (reader + command + connection lease). Dispose the scope, not the reader, when you’re done. Reach for it when you need:

- Manual control over `DbDataReader` (call `NextResult`, `GetSchemaTable`, etc.).
- To hand the live reader to downstream components (CSV export, streaming transformations) without buffering.

### 4.7 Oracle-specific APIs (`DatabaseHelper.Oracle.cs`)

`ExecuteRefCursorAsync/ExecuteRefCursor` run Oracle packages that expose REF CURSOR OUT parameters. Supply a `DbCommandRequest` where the cursor parameter is defined via `OracleParameterHelper.RefCursor("p_cursor")` and pass the logical name (`cursorParameterName`). While the returned `DbReaderScope` is alive you can also inspect `scope.Command.Parameters` to open additional cursor outputs with `OracleRefCursorReaderFactory.Create(parameter)` before disposing the scope.

---

## 5. Multi-result & stored procedure strategies

| Provider | Pattern | Stored procedure example | Notes |
|----------|---------|--------------------------|-------|
| SQL Server | Native multi-result sets via `DbDataReader.NextResult` or `LoadDataSet*`. | `dbo.User_GetAndOrders` returns two SELECTs. | Use `ExecuteStoredProcedureReader*` or `LoadDataSetAsync`. |
| PostgreSQL | Emulate multi-result via `PostgresCursorHelper.BuildMultiCursorRequest` or have procedures open cursors and `FETCH ALL`. | `app.get_user_and_orders` -> wrap each SELECT in helper. | Each SELECT becomes its own `DataTable` in `LoadDataSetAsync`. |
| Oracle | REF CURSOR outputs per dataset. | `pkg_user.get_user_and_orders` returning `p_user` and `p_orders`. | Define two REF CURSOR outputs; call `ExecuteRefCursorAsync` for each or open remaining cursors from `DbReaderScope.Command`. |

---

## 6. Validation, normalization, and duplicate detection

1. `DbCommandRequestValidator` enforces non-empty command text, reasonable timeouts, and `TraceName` length.
2. `DbParameterDefinitionValidator` ensures names exist, list parameters have values, and temporal/decimal values fall inside `InputNormalizationOptions` bounds.
3. `ValueNormalizer` coerces `DateTime`, `DateTimeOffset`, `DateOnly`, `decimal`, and arrays into provider-safe shapes (UTC, rounded decimals, element-wise validation).
4. Additional binder checks guarantee `Size > 0`, precision/scale ranges, and sanitize provider type names.

**Failure example**

```csharp
var request = new DbCommandRequest
{
    CommandText = "dbo.User_Update",
    Parameters = new[]
    {
        DbParameter.Input("DisplayName", new string('x', 65), DbType.String, size: 64),   // value too long
        DbParameter.Input("CreditLimit", 999999999999m, DbType.Decimal, precision: 10, scale: 2), // exceeds precision
        DbParameter.Input("UserId", Guid.Empty, DbType.Guid)
    }
};

await helper.ExecuteAsync(request);
```

Result: `ValidationException` (or `ArgumentOutOfRangeException`) describing both violations before any provider call. If you set `Validate = false`, provider errors occur instead (`SqlException: String or binary data would be truncated.`).

---

## 7. Exception & resilience policy

| `WrapProviderExceptions` | What you receive | Typical use |
|-------------------------|------------------|-------------|
| `true` (default) | `DataAccessLayer.Exceptions.DataException` with the original provider exception as `InnerException`. | Application-wide logging/telemetry sees a consistent exception type. |
| `false` | Raw provider exception (`SqlException`, `NpgsqlException`, `OracleException`). | Debugging specific provider codes. |

Per-request override:

```csharp
var overrideOptions = new DatabaseOptions
{
    Provider = databaseOptions.Provider,
    ConnectionString = databaseOptions.ConnectionString,
    WrapProviderExceptions = false
};

var request = new DbCommandRequest
{
    CommandText = "dbo.User_Update",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[] { DbParameter.Input("UserId", userId, DbType.Guid) },
    OverrideOptions = overrideOptions
};
```

Every execution also flows through `_resilience.CommandAsyncPolicy` / `_resilience.CommandSyncPolicy`, so retries/timeout budgets apply uniformly, including Oracle’s sync-only provider.

---

## 8. Metadata-sensitive parameter recipes

### SQL Server (TVPs, outputs, strings)

```csharp
var sqlParams = new[]
{
    DbParameter.Input("UserId", userId, DbType.Guid),
    DbParameter.Input("Email", payload.Email, DbType.String, size: 256, isNullable: false),
    DbParameter.Input("IsActive", payload.IsActive, DbType.Boolean),
    DbParameter.Input("CreditLimit", payload.CreditLimit, DbType.Decimal, precision: 18, scale: 2),
    StructuredParameterBuilder.SqlServerTableValuedParameter("UserIds", payload.TvpRows, "dbo.UserIdList"),
    DbParameter.Output("RowsTouched", DbType.Int32),
    DbParameter.ReturnValue(DbType.Int32)
};
```

### PostgreSQL (arrays, provider types)

```csharp
var pgParams = new[]
{
    DbParameter.Input("user_id", userId, DbType.Guid),
    DbParameter.Input("last_login", DateTimeOffset.UtcNow, DbType.DateTimeOffset),
    DbParameter.Input("is_active", payload.IsActive, DbType.Boolean),
    DbParameter.InputList("role_ids", payload.RoleIds, DbType.Guid, providerTypeName: "_uuid"),
    DbParameter.Output("rows_out", DbType.Int64)
};
```

### Oracle (RAW(16), NUMBER(1), REF CURSOR)

```csharp
var oracleParams = new[]
{
    DbParameter.Input("p_user_id", userId, DbType.Guid),              // binder converts Guid -> RAW(16)
    DbParameter.Input("p_is_active", payload.IsActive, DbType.Boolean), // coerced to NUMBER(1)
    DbParameter.Input("p_credit_limit", payload.CreditLimit, DbType.Decimal, precision: 18, scale: 2),
    DbParameter.Input("p_notes", payload.Notes, DbType.String, size: 4000, isNullable: true),
    OracleParameterHelper.RefCursor("p_user_cursor"),
    OracleParameterHelper.RefCursor("p_orders_cursor")
};
```

String length enforcement remains provider-specific: if you specify `size: 20` but send 21 characters, SQL Server throws `SqlException` and Oracle throws `ORA-01401`. Leave `isNullable: true` when the database column permits nulls to avoid unnecessary validation failures.

---

## 9. Handling large payloads and multi-dataset reads (recipes)

| Need | Best choice | Example |
|------|-------------|---------|
| Stream huge rowset | `StreamAsync<T>` | ```csharp\nawait foreach (var row in helper.StreamAsync(request, Map, token)) { ... }\n``` |
| Dump BLOB/CLOB to disk | `StreamColumnAsync` / `StreamTextAsync` | ```csharp\nawait helper.StreamColumnAsync(request, 0, destinationStream);\n``` |
| Buffer multiple tables (SQL Server) | `ExecuteStoredProcedureReaderAsync` + `NextResultAsync` OR `LoadDataSetAsync` | ```csharp\nawait using var scope = await helper.ExecuteStoredProcedureReaderAsync("dbo.User_GetAndOrders");\n``` |
| Multi-result on PostgreSQL 8.x | `PostgresCursorHelper.BuildMultiCursorRequest` + `LoadDataSetAsync` | ```csharp\nvar cursorRequest = PostgresCursorHelper.BuildMultiCursorRequest(selects, parameters);\nvar ds = await helper.LoadDataSetAsync(cursorRequest);\n``` |
| Oracle multi-result | `ExecuteRefCursorAsync` / `LoadRefCursorDataSetAsync` | ```csharp\nawait using var scope = await helper.ExecuteRefCursorAsync(request, "p_orders");\n``` |

Tips:
- Stay inside a single `DbReaderScope` for Oracle REF CURSORs until every cursor parameter is consumed.
- When memory matters, prefer `Stream*` APIs or raw `DbReaderScope` instead of `LoadDataSet`.

---

## 10. Optional parameters and validation overrides

- Procedures with default values: simply omit the parameter. The helper only validates the parameters you include.
- Optional-but-present parameters: set `isNullable: true` or provide `defaultValue`.
- Mixed validation: keep `Validate = true` globally, but you can set `request.Validate = false` when you must send intentionally out-of-range data (rare). Document why in code comments when you do this.

---

## 11. Provider capability matrix

| Capability | SQL Server 2005 | PostgreSQL 8.x | Oracle 9 |
|------------|-----------------|----------------|----------|
| Stored procedure OUT params | Native | Emulated via SELECT rewrite | Native |
| Multi-result sets | Native | Cursor helper / FETCH ALL | REF CURSOR outputs |
| Arrays / list parameters | TVP (`StructuredParameterBuilder`) | `_type` arrays via `TreatAsList` | Array binding via `StructuredParameterBuilder.OracleArray` |
| Streaming | Fully async | Fully async | Async wrappers over sync provider |
| Boolean support | `bit` | `boolean` | coerced to `NUMBER(1)` |
| Guid support | `uniqueidentifier` | `uuid` | RAW(16) conversion |
| Exception wrapping | `WrapProviderExceptions` | `WrapProviderExceptions` | `WrapProviderExceptions` |

---

## 12. References & tests

- Implementation: `DataAccessLayer/Common/DbHelper/DatabaseHelper/*.cs` (Commands, Queries, Streaming, StoredProcedures, ReaderScopes, Oracle, Core).
- Parameter helpers: `DataAccessLayer/Common/DbHelper/Execution/Parameters/*.cs`.
- Provider helpers: `DataAccessLayer/Common/DbHelper/Providers/**` (Postgres cursors, Oracle readers).
- Validation: `DataAccessLayer/Common/DbHelper/Validation/*.cs`.
- Tests: `tests/DataAccessLayer.Tests/DatabaseHelper/**` (feature folders for commands, queries, streaming, validation, providers).

Keep this document updated whenever you add a method to any partial, introduce a new provider feature, or extend the metadata surface so that consumers always have a complete, provider-agnostic reference.
