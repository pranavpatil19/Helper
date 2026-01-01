## DatabaseHelper Provider Guide (SQL Server 2005 / PostgreSQL 8.x / Oracle 9)

Use this guide when you need to wire `DataAccessLayer.Common.DbHelper.DatabaseHelper` into real workloads. Every example shows a fully populated `DbCommandRequest` (command text/type, timeout, trace name, parameters) so you can copy it and adjust values without guessing.

---

## 1. Provider snapshot

| Provider   | Minimum version | DAL highlights |
|------------|-----------------|----------------|
| SQL Server | 2005            | Native multi-result sets, stored procedures, TVPs, savepoints (`SAVE TRANSACTION`). |
| PostgreSQL | 8.x             | OUT parameter emulation via SELECT rewrite, multi-result via `PostgresCursorHelper`, arrays through `TreatAsList`. |
| Oracle     | 9               | REF CURSOR pipelines, async wrappers over sync ODP.NET, array binding + savepoints (`SAVEPOINT foo`). |

---

## 2. Quick start (complete request)

```csharp
var request = new DbCommandRequest
{
    CommandText = "dbo.User_UpdateLogin",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("UserId", userId, DbType.Guid),
        DbParameter.Input("LastLoginUtc", DateTimeOffset.UtcNow, DbType.DateTimeOffset),
        DbParameter.Input("IsActive", payload.IsActive, DbType.Boolean),
        DbParameter.Output("RowsTouched", DbType.Int32),
        DbParameter.ReturnValue(DbType.Int32)
    },
    CommandTimeoutSeconds = 30,
    TraceName = "user.update-login",
    Validate = true
};

await using var dal = DalHelperFactory.Create(databaseOptions);
var result = await dal.DatabaseHelper.ExecuteAsync(request);
Console.WriteLine($"Rows: {result.RowsAffected}, OUT RowsTouched: {result.OutputParameters["RowsTouched"]}");
```

Key behaviors:

- Validation (`DbCommandRequest.Validate = true`) runs FluentValidation + binder inspections (size, precision, list metadata).
- `DatabaseOptions.WrapProviderExceptions` governs wrapping in `DataException`.
- Telemetry is emitted via `IDataAccessTelemetry` + `ActivitySource("Helper.DataAccessLayer.Database")`, leveraging `TraceName`.

---

## 3. Parameter metadata cheatsheet

| Metadata | Why it matters | Provider notes |
|----------|----------------|----------------|
| `Name` | Logical name without `@`/`:` prefixes. | Required everywhere; DAL adds provider prefixes. |
| `DbType` | Explicit CLR → provider mapping. | Avoids inference mismatch (e.g., bool vs numeric). |
| `Direction` | Input, Output, InputOutput, ReturnValue. | PostgreSQL OUT becomes SELECT rewrite; Oracle uses REF CURSOR. |
| `Size` | String/binary length > 0. | SQL Server/Oracle enforce; Postgres stores as varchar/text. |
| `Precision`/`Scale` | Decimal metadata. | Enforced (precision 1–38, scale ≤ precision). |
| `ProviderTypeName` | Provider alias (`_uuid`, `jsonb`, `RefCursor`). | Sanitized unless `ParameterBinding.AllowUnsafeProviderTypeNames` is true. |
| `TreatAsList`/`Values` | List/array semantics. | SQL Server -> TVP; Postgres -> arrays; Oracle -> VARRAY. |
| `IsNullable` | Document intent for `null`. | Avoids validators flagging missing values. |
| `DefaultValue` | Replace `null` before binding. | Useful for sentinel values across providers. |

### Common `DbType` mapping

| Logical type | SQL Server | PostgreSQL | Oracle |
|--------------|------------|------------|--------|
| `DbType.Guid` | `uniqueidentifier` | `uuid` | `RAW(16)` (DAL converts) |
| `DbType.Boolean` | `bit` | `boolean` | `NUMBER(1)` |
| `DbType.DateTimeOffset` | `datetimeoffset` | `timestamptz` (stored as UTC) | `TIMESTAMP WITH TIME ZONE` (converted to UTC) |
| `DbType.Decimal` | `decimal(p,s)` | `numeric(p,s)` | `NUMBER(p,s)` |
| `DbType.String` | `nvarchar(size)` / `nvarchar(max)` | `varchar(size)` / `text` | `NVARCHAR2(size)` |
| `DbType.Binary` | `varbinary(size)` / `varbinary(max)` | `bytea` | `RAW(size)` / `BLOB` |
| `DbType.Object` | TVPs / XML | Arrays (`_uuid`, `_int4`), JSONB | REF CURSOR, array binding |

---

## 4. Provider playbooks

### 4.1 SQL Server

- Stored procedures, OUTPUT parameters, TVPs, savepoints, and multi-result readers are native features.
- `ExecuteAsync` / `ExecuteScalarAsync` call `SqlCommand.ExecuteNonQuery` / `ExecuteScalar`.
- Use table-valued parameters via `StructuredParameterBuilder.SqlServerTableValuedParameter`.

```csharp
var updateRequest = new DbCommandRequest
{
    CommandText = "dbo.User_UpdateDisplayName",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("UserId", userId, DbType.Guid),
        DbParameter.Input("DisplayName", payload.DisplayName, DbType.String, size: 64, isNullable: false),
        DbParameter.Input("RoleIds", payload.RoleIds, DbType.Guid, providerTypeName: "dbo.RoleIdList"),
        DbParameter.Output("RowsTouched", DbType.Int32)
    },
    CommandTimeoutSeconds = 30,
    TraceName = "user.update-display-name"
};

var updateResult = await dal.DatabaseHelper.ExecuteAsync(updateRequest);
```

### 4.2 PostgreSQL

- OUT parameters are rewritten into SELECT plans (`PostgresOutParameterPlan`).
- Multi-result flows rely on `PostgresCursorHelper.BuildMultiCursorRequest`.
- Arrays require `TreatAsList` + provider type name (e.g., `_uuid`).

```csharp
var procRequest = new DbCommandRequest
{
    CommandText = "call app.update_user(@user_id, @display_name, @rows_out OUT)",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("user_id", userId, DbType.Guid),
        DbParameter.Input("display_name", payload.DisplayName, DbType.String, size: 64),
        DbParameter.Output("rows_out", DbType.Int32)
    },
    CommandTimeoutSeconds = 45,
    TraceName = "pg.update-user"
};

var procResult = await dal.DatabaseHelper.ExecuteAsync(procRequest);
```

Multi-result cursor example:

```csharp
var cursorPlan = PostgresCursorHelper.BuildMultiCursorRequest(
    new[]
    {
        "SELECT id, email FROM app.users WHERE id = @user_id",
        "SELECT id, total FROM app.orders WHERE user_id = @user_id"
    },
    new[]
    {
        DbParameter.Input("user_id", userId, DbType.Guid)
    },
    traceName: "pg.user-and-orders");

var cursorRequest = new DbCommandRequest
{
    CommandText = cursorPlan.CommandText,
    CommandType = cursorPlan.CommandType,
    Parameters = cursorPlan.Parameters,
    TraceName = cursorPlan.TraceName,
    CommandTimeoutSeconds = 60
};

var userAndOrders = await dal.DatabaseHelper.LoadDataSetAsync(cursorRequest);
```

### 4.3 Oracle

- Managed ODP.NET async methods are sync under the covers; DAL routes accordingly.
- REF CURSORs are exposed via output parameters + `ExecuteRefCursor*`/`LoadRefCursor*`.
- Savepoints use `SAVEPOINT foo` / `ROLLBACK TO SAVEPOINT foo`.

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
    TraceName = "oracle.get-profile"
};

await using var profileScope = await dal.DatabaseHelper.ExecuteRefCursorAsync(oracleRequest, "p_profile_cursor");
await using var reader = profileScope.Reader;
while (reader.Read())
{
    // map fields
}
```

---

## 5. Feature matrix (per provider)

| Feature | SQL Server | PostgreSQL | Oracle |
|---------|------------|------------|--------|
| Commands (`Execute*`) | Native | OUT params rewritten to SELECT | Native (async wraps sync) |
| Scalars (`ExecuteScalar*`) | Native | OUT rewrite handles scalar row | Native |
| Buffered queries (`Query*`, `LoadDataTable*`) | Native multi-result via `NextResult` | Single result; use cursor helper for multiples | Native multi-result |
| Streaming (`StreamAsync`, `StreamColumn*`, `StreamText*`) | Fully async | Fully async | Async wrappers over sync provider |
| Stored procedures helpers | `ExecuteStoredProcedure*` families | Works; rely on functions + cursor helper for multi-result | Works; REF CURSOR APIs |
| Savepoints | `SAVE TRANSACTION` / `ROLLBACK TRANSACTION` | `SAVEPOINT` / `ROLLBACK TO SAVEPOINT` / `RELEASE` | `SAVEPOINT` / `ROLLBACK TO SAVEPOINT` |

---

## 6. Multi-result recipes (cross-provider)

| Provider | Recipe |
|----------|--------|
| SQL Server | ```csharp\nawait using var scope = await dal.DatabaseHelper.ExecuteStoredProcedureReaderAsync(\"dbo.User_GetAndOrders\", new[] { DbParameter.Input(\"UserId\", userId, DbType.Guid) });\nawait using var reader = scope.Reader;\nwhile (await reader.ReadAsync()) { /* first result */ }\nawait reader.NextResultAsync();\nwhile (await reader.ReadAsync()) { /* second result */ }\n``` |
| PostgreSQL | ```csharp\nvar cursorRequest = PostgresCursorHelper.BuildMultiCursorRequest(selects, sharedParameters, traceName: \"pg.user-orders\");\nvar dataSet = await dal.DatabaseHelper.LoadDataSetAsync(cursorRequest);\n``` |
| Oracle | ```csharp\nvar request = new DbCommandRequest\n{\n    CommandText = \"pkg_user.get_user_and_orders\",\n    CommandType = CommandType.StoredProcedure,\n    Parameters = new[]\n    {\n        DbParameter.Input(\"p_user_id\", userId, DbType.Guid),\n        DbParameter.Output(\"p_user_cursor\", DbType.Object, providerTypeName: \"RefCursor\"),\n        DbParameter.Output(\"p_orders_cursor\", DbType.Object, providerTypeName: \"RefCursor\")\n    },\n    CommandTimeoutSeconds = 60,\n    TraceName = \"oracle.user-orders\"\n};\nvar ds = await dal.DatabaseHelper.LoadRefCursorDataSetAsync(request, \"p_user_cursor\", \"p_orders_cursor\");\n``` |

---

## 7. Streaming & large payload patterns

| Scenario | Request | API |
|----------|---------|-----|
| Stream BLOB to disk (SQL Server) | ```csharp\nvar documentRequest = new DbCommandRequest\n{\n    CommandText = \"SELECT Document FROM dbo.Files WHERE Id = @Id\",\n    CommandType = CommandType.Text,\n    Parameters = new[] { DbParameter.Input(\"Id\", fileId, DbType.Guid) },\n    CommandBehavior = CommandBehavior.SequentialAccess,\n    CommandTimeoutSeconds = 180,\n    TraceName = \"files.stream\"\n};\nawait using var destination = File.Create(path);\nvar bytes = await dal.DatabaseHelper.StreamColumnAsync(documentRequest, 0, destination);\n``` | `StreamColumnAsync` |
| Stream large rowset (PostgreSQL) | ```csharp\nvar streamRequest = new DbCommandRequest\n{\n    CommandText = \"SELECT id, total FROM app.orders WHERE user_id = @UserId ORDER BY id\",\n    CommandType = CommandType.Text,\n    Parameters = new[] { DbParameter.Input(\"UserId\", userId, DbType.Guid) },\n    CommandBehavior = CommandBehavior.SequentialAccess,\n    CommandTimeoutSeconds = 120,\n    TraceName = \"orders.stream\"\n};\nawait foreach (var dto in dal.DatabaseHelper.StreamAsync(streamRequest, r => new OrderDto(r.GetGuid(0), r.GetDecimal(1))))\n{\n    // consume row\n}\n``` | `StreamAsync<T>` |
| Oracle REF CURSOR streaming | See section 4.3 example. | `ExecuteRefCursorAsync` / `LoadRefCursorAsync` |

---

## 8. Validation, telemetry, and exception handling

- **Validation**: `DbCommandRequestValidator` + `DbParameterDefinitionValidator` ensure every request has command text, reasonable timeouts, and fully described parameters. `ValueNormalizer` enforces date/decimal ranges from `InputNormalizationOptions`.
- **Telemetry**: Each helper call wraps execution in `ExecuteWithRequestActivity*`, emitting spans with operation names (`ExecuteAsync`, `QueryAsync`, etc.), `TraceName`, rows affected, bytes streamed.
- **Resilience / exception strategy**: `ExecuteWithResilience*` uses the configured Polly policies (retry/timeouts). `WrapProviderExceptions` (global or per request via `OverrideOptions`) toggles between `DataException` and raw provider exceptions.

---

## 9. Reference map

| Artifact | Purpose |
|----------|---------|
| `docs/DatabaseHelper/DatabaseHelper.md` | API-by-API cheat sheet (commands, queries, streaming, stored procedures). |
| `docs/DatabaseHelper/Transactions.md` | Transaction scopes, savepoints, suppression, recipes. |
| `DataAccessLayer/Common/DbHelper/DatabaseHelper/*.cs` | Implementation (Commands, Queries, Streaming, StoredProcedures, Oracle, Core). |
| `DataAccessLayer/Common/DbHelper/Execution/Parameters/*.cs` | Parameter definitions, structured builders, normalization. |
| `DataAccessLayer/Common/DbHelper/Providers/Postgres` | Cursor helper, OUT parameter plans. |
| `DataAccessLayer/Common/DbHelper/Providers/Oracle` | REF CURSOR reader factory, helpers. |
| `tests/DataAccessLayer.Tests/DatabaseHelper/**` | Feature tests grouped by provider, validation, and streaming. |

Keep this guide and the API playbooks synchronized—whenever you add a DatabaseHelper feature or change provider behavior, update both references so teams always know how to call the APIs in a provider-agnostic, fully specified way.
