## Transaction Manager Playbook (SQL Server 2005 / PostgreSQL 8 / Oracle 9)

Example-first guide for everything under `DataAccessLayer/Common/DbHelper/Transactions`. Keep it nearby when you need to start a scope, choose the right option, or copy a ready-made pattern (single proc, all-or-nothing, partial commit, savepoints, suppressed scopes).

---

## 1. Quick start (async + sync)

```csharp
var insertUserRequest = new DbCommandRequest
{
    CommandText = "dbo.User_Insert",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("UserId", userId, DbType.Guid),
        DbParameter.Input("Email", payload.Email, DbType.String, size: 256),
        DbParameter.Input("IsActive", payload.IsActive, DbType.Boolean),
        DbParameter.Output("RowsTouched", DbType.Int32)
    },
    CommandTimeoutSeconds = 30,
    TraceName = "user.insert",
    Validate = true
};

await using var dal = DalHelperFactory.Create(options);
await using var tx = await dal.TransactionManager.BeginAsync(); // ReadCommitted + Required

await dal.DatabaseHelper.ExecuteAsync(insertUserRequest);
await tx.CommitAsync(); // persist changes
```

Sync counterpart:

```csharp
using var dal = DalHelperFactory.Create(options);
using var tx = dal.TransactionManager.Begin();

dal.DatabaseHelper.Execute(insertUserRequest);
tx.Commit();
```

Skip `Commit*` and the scope rolls back automatically during `Dispose*` (a warning is logged).

---

## 2. Cheat sheet (copy/paste)

| Task | Snippet | Notes |
|------|---------|-------|
| Default scope | `await using var tx = await dal.TransactionManager.BeginAsync();` | ReadCommitted + `Required`. |
| Force independent scope | `await using var tx = await dal.TransactionManager.BeginAsync(scopeOption: TransactionScopeOption.RequiresNew);` | Always opens a new connection + transaction. |
| Suppress transactions | `await using var tx = await dal.TransactionManager.BeginAsync(scopeOption: TransactionScopeOption.Suppress);` | `tx.Transaction == null`; savepoints disabled. |
| Run code with auto-commit | `await dal.TransactionManager.WithTransactionAsync(async (scope, token) => { ... });` | Extension wraps begin/commit/rollback + disposal. |
| Savepoint guard | `await tx.BeginSavepointAsync("user_upsert"); ... await tx.RollbackToSavepointAsync("user_upsert");` | SQL Server, PostgreSQL, Oracle supported. |

All DAL helper calls automatically reuse the ambient scope through `TransactionScopeAmbient` unless you explicitly supply `DbConnection`/`DbTransaction`.

---

## 3. Scope options at a glance

### `TransactionScopeOption.Required` (default)
- Reuses the current ambient transaction; creates one only if none exists.
- Ideal for nested units of work that must commit/rollback with the parent.

```csharp
await using var outer = await manager.BeginAsync();
await using var inner = await manager.BeginAsync(scopeOption: TransactionScopeOption.Required);
await inner.CommitAsync(); // marks inner complete
await outer.CommitAsync();
```

### `TransactionScopeOption.RequiresNew`
- Always starts a brand-new connection + transaction, even inside an ambient scope.
- Use for outbox/inbox patterns or operations that must succeed independently.

```csharp
var auditRequest = new DbCommandRequest
{
    CommandText = "dbo.Audit_Insert",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("EventId", Guid.NewGuid(), DbType.Guid),
        DbParameter.Input("Message", auditMessage, DbType.String, size: 256)
    },
    CommandTimeoutSeconds = 15,
    TraceName = "audit.insert-single"
};

await using var singleUse = await manager.BeginAsync(scopeOption: TransactionScopeOption.RequiresNew);
await dal.DatabaseHelper.ExecuteAsync(auditRequest);
await singleUse.CommitAsync();
```

### `TransactionScopeOption.Suppress`
- Opens a connection with no transaction (`Transaction == null`).
- Great for logging, DDL, or maintenance statements that must not roll back with ambient work.

```csharp
await using var suppressed = await manager.BeginAsync(scopeOption: TransactionScopeOption.Suppress);

var logRequest = new DbCommandRequest
{
    CommandText = "INSERT INTO dbo.Logs (Message) VALUES (@Message)",
    CommandType = CommandType.Text,
    Parameters = new[] { DbParameter.Input("Message", text, DbType.String, size: 4000) },
    Connection = suppressed.Connection,
    Transaction = suppressed.Transaction,
    CloseConnection = false,
    CommandTimeoutSeconds = 15,
    TraceName = "logs.insert"
};

await dal.DatabaseHelper.ExecuteAsync(logRequest);
```

Tips:
- Disposing without `Commit*` always rolls back (warning logged).
- Suppressed scopes throw `TransactionFeatureNotSupportedException` if you call savepoint APIs or require savepoints.

---

## 4. Transaction recipes

### 5.1 Single stored procedure

```csharp
await using var tx = await dal.TransactionManager.BeginAsync();
await dal.DatabaseHelper.ExecuteAsync(insertUserRequest);
await tx.CommitAsync();
```

Skip `CommitAsync()`? The insert is rolled back when `tx` is disposed.

### 5.2 All-or-nothing (insert + update)

```csharp
var insertUserRequest = new DbCommandRequest
{
    CommandText = "dbo.User_Insert",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("UserId", userId, DbType.Guid),
        DbParameter.Input("Email", payload.Email, DbType.String, size: 256),
        DbParameter.Input("IsActive", payload.IsActive, DbType.Boolean)
    },
    CommandTimeoutSeconds = 30,
    TraceName = "user.insert"
};

var updateInventoryRequest = new DbCommandRequest
{
    CommandText = "dbo.Inventory_UpdateQuantity",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("ProductId", payload.ProductId, DbType.Guid),
        DbParameter.Input("QuantityDelta", payload.QuantityDelta, DbType.Int32),
        DbParameter.Output("NewQuantity", DbType.Int32)
    },
    CommandTimeoutSeconds = 30,
    TraceName = "inventory.update-qty"
};

await using var tx = await dal.TransactionManager.BeginAsync();

try
{
    await dal.DatabaseHelper.ExecuteAsync(insertUserRequest);
    await dal.DatabaseHelper.ExecuteAsync(updateInventoryRequest);
    await tx.CommitAsync();
}
catch (ValidationException)
{
    await tx.RollbackAsync(); // optional; dispose without commit also rolls back
    throw;
}
```

### 5.3 Partial commit (first step must persist even if second fails)

```csharp
var insertAuditRequest = new DbCommandRequest
{
    CommandText = "dbo.Audit_Insert",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("EventId", Guid.NewGuid(), DbType.Guid),
        DbParameter.Input("UserId", userId, DbType.Guid),
        DbParameter.Input("Action", "BalanceUpdate", DbType.String, size: 64),
        DbParameter.Input("OccurredUtc", DateTimeOffset.UtcNow, DbType.DateTimeOffset)
    },
    CommandTimeoutSeconds = 15,
    TraceName = "audit.insert"
};

var updateUserRequest = new DbCommandRequest
{
    CommandText = "dbo.User_UpdateProfile",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("UserId", userId, DbType.Guid),
        DbParameter.Input("DisplayName", payload.DisplayName, DbType.String, size: 64)
    },
    CommandTimeoutSeconds = 30,
    TraceName = "user.update-profile"
};

var updateBalanceRequest = new DbCommandRequest
{
    CommandText = "dbo.Account_UpdateBalance",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("AccountId", payload.AccountId, DbType.Guid),
        DbParameter.Input("Amount", payload.Amount, DbType.Decimal, precision: 18, scale: 2),
        DbParameter.Output("NewBalance", DbType.Decimal, precision: 18, scale: 2)
    },
    CommandTimeoutSeconds = 60,
    TraceName = "account.update-balance"
};

await using (var auditScope = await dal.TransactionManager.BeginAsync(scopeOption: TransactionScopeOption.RequiresNew))
{
    await dal.DatabaseHelper.ExecuteAsync(insertAuditRequest);
    await auditScope.CommitAsync(); // survives regardless of outer failure
}

await using var tx = await dal.TransactionManager.BeginAsync();

try
{
    await dal.DatabaseHelper.ExecuteAsync(updateUserRequest);
    await dal.DatabaseHelper.ExecuteAsync(updateBalanceRequest);
    await tx.CommitAsync();
}
catch
{
    await tx.RollbackAsync();
    throw;
}
```

### 5.4 Validation before commit

```csharp
var calculateTotalsRequest = new DbCommandRequest
{
    CommandText = "dbo.Invoice_CalculateTotals",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("InvoiceId", invoiceId, DbType.Guid),
        DbParameter.Output("LineCount", DbType.Int32)
    },
    CommandTimeoutSeconds = 45,
    TraceName = "invoice.calculate-totals"
};

await using var tx = await dal.TransactionManager.BeginAsync();

var totals = await dal.DatabaseHelper.ExecuteAsync(calculateTotalsRequest);
if (totals.RowsAffected == 0)
{
    await tx.RollbackAsync(); // frees locks immediately
    throw new InvalidOperationException("Totals were not generated.");
}

await tx.CommitAsync();
```

---

## 5. Savepoints (provider matrix + example)

| Provider | Begin | Rollback | Release |
|----------|-------|----------|---------|
| SQL Server 2005 | `SAVE TRANSACTION foo` | `ROLLBACK TRANSACTION foo` | Auto-release on commit. |
| PostgreSQL 8.x | `SAVEPOINT foo` | `ROLLBACK TO SAVEPOINT foo` | `RELEASE SAVEPOINT foo`. |
| Oracle 9 | `SAVEPOINT foo` | `ROLLBACK TO SAVEPOINT foo` | Auto-release on commit. |

```csharp
var insertUserRequest = new DbCommandRequest
{
    CommandText = "dbo.User_Insert",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("UserId", userId, DbType.Guid),
        DbParameter.Input("Email", payload.Email, DbType.String, size: 256)
    },
    CommandTimeoutSeconds = 30,
    TraceName = "user.insert"
};

var insertPreferencesRequest = new DbCommandRequest
{
    CommandText = "dbo.UserPreference_Insert",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("UserId", userId, DbType.Guid),
        DbParameter.Input("SendNewsletters", payload.Preferences.SendNewsletters, DbType.Boolean)
    },
    CommandTimeoutSeconds = 30,
    TraceName = "user-pref.insert"
};

await tx.BeginSavepointAsync("user_upsert");
try
{
    await dal.DatabaseHelper.ExecuteAsync(insertUserRequest);
    await dal.DatabaseHelper.ExecuteAsync(insertPreferencesRequest);
    await tx.ReleaseSavepointAsync("user_upsert"); // no-op on SQL Server / Oracle
}
catch
{
    await tx.RollbackToSavepointAsync("user_upsert");
    throw;
}
```

Rules:
- Names allow letters, digits, `_`, `-` only.
- Suppressed scopes cannot use savepoints.

---

## 6. Suppressed scopes (work outside ambient transaction)

```csharp
await using var suppressed = await dal.TransactionManager.BeginAsync(scopeOption: TransactionScopeOption.Suppress);

var telemetryRequest = new DbCommandRequest
{
    CommandText = "INSERT INTO dbo.Logs ...",
    Connection = suppressed.Connection,
    Transaction = suppressed.Transaction, // null
    CloseConnection = false
};

await dal.DatabaseHelper.ExecuteAsync(telemetryRequest);
// No commit/rollback required; disposing the scope simply closes the connection.
```

Remember: `Commit*`/`Rollback*` are no-ops here, and savepoint methods throw.

---

## 7. Auto-managed pattern (`WithTransactionAsync`)

```csharp
var insertHeaderRequest = new DbCommandRequest
{
    CommandText = "dbo.Order_InsertHeader",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("OrderId", orderId, DbType.Guid),
        DbParameter.Input("CustomerId", customerId, DbType.Guid)
    },
    CommandTimeoutSeconds = 30,
    TraceName = "order.insert-header"
};

var insertLinesRequest = new DbCommandRequest
{
    CommandText = "dbo.Order_InsertLines",
    CommandType = CommandType.StoredProcedure,
    Parameters = new[]
    {
        DbParameter.Input("OrderId", orderId, DbType.Guid),
        DbParameter.Input("LinesJson", linesJson, DbType.String, size: 4000)
    },
    CommandTimeoutSeconds = 60,
    TraceName = "order.insert-lines"
};

await dal.TransactionManager.WithTransactionAsync(
    async (scope, token) =>
    {
        await dal.DatabaseHelper.ExecuteAsync(insertHeaderRequest, token);
        await dal.DatabaseHelper.ExecuteAsync(insertLinesRequest, token);
        // auto-commit when delegate finishes successfully
    },
    isolationLevel: IsolationLevel.Serializable,
    scopeOption: TransactionScopeOption.Required);
```

Behavior:
- Extension acquires a scope, runs your delegate, commits on success, rolls back if the delegate throws, and disposes.
- Nested calls still reuse the ambient scope because `scopeOption` defaults to `Required`.

---

## 8. Ambient behavior & resilience (fast facts)

- `TransactionScopeAmbient` maintains an `AsyncLocal` stack. `ConnectionScopeManager` pulls from it so every DAL helper automatically enlists in the current transaction.
- Asynchronous awaits keep the same ambient scope; failing to `Commit` a dependent scope (`Required` inside ambient) causes the parent to roll back when the dependent is disposed.
- `TransactionScope` wraps `Commit*`/`Rollback*` in the configured Polly policies (`DatabaseOptions.Resilience`).
- `DependentTransactionScope` mirrors the parent. `SuppressedTransactionScope` just owns an open connection and closes it on dispose.

---

## 9. DatabaseHelper integration

| Situation | Behavior | Action |
|-----------|----------|--------|
| Execute helpers inside a transaction | Command reuses ambient connection/transaction automatically. | Just call `ExecuteAsync`, `QueryAsync`, etc. |
| Need to run outside ambient scope | Ambient scope exists but this call should not enlist. | Start `Suppress`, pass its connection/transaction (null) to `DbCommandRequest`. |
| Bulk helpers require a transaction | `BulkWriteHelper` checks `TransactionScopeAmbient.Current`. | Begin a scope before calling bulk APIs. |

---

## 10. Advanced combos & testing

### 10.1 Savepoint + suppressed logging + partial commit

```csharp
// Step 1: Always log to audit (RequiresNew)
await using (var auditScope = await dal.TransactionManager.BeginAsync(TransactionScopeOption.RequiresNew))
{
    await dal.DatabaseHelper.ExecuteAsync(insertAuditRequest);
    await auditScope.CommitAsync();
}

// Step 2: Main business transaction with savepoint
await using var tx = await dal.TransactionManager.BeginAsync();
await tx.BeginSavepointAsync("business");

try
{
    await dal.DatabaseHelper.ExecuteAsync(updateUserRequest);
    await dal.DatabaseHelper.ExecuteAsync(updateBalanceRequest);
    await tx.ReleaseSavepointAsync("business");

    // Step 3: Suppressed scope for telemetry/logging
    await using var suppressed = await dal.TransactionManager.BeginAsync(TransactionScopeOption.Suppress);
    var logRequest = new DbCommandRequest
    {
        CommandText = "INSERT INTO dbo.Logs (Message) VALUES (@Message)",
        CommandType = CommandType.Text,
        Parameters = new[] { DbParameter.Input("Message", "Balance updated", DbType.String, size: 256) },
        Connection = suppressed.Connection,
        Transaction = suppressed.Transaction,
        CloseConnection = false,
        CommandTimeoutSeconds = 15,
        TraceName = "logs.insert-business"
    };
    await dal.DatabaseHelper.ExecuteAsync(logRequest);

    await tx.CommitAsync();
}
catch
{
    await tx.RollbackToSavepointAsync("business");
    await tx.RollbackAsync();
    throw;
}
```

Result: the audit entry survives even if the business work fails, savepoints make retries cheap, and telemetry never blocks the commit path because it runs outside the ambient transaction.

### 10.2 Testing references

| Behavior | Tests to review |
|----------|-----------------|
| Scope options, ambient reuse, `RequiresNew` | `tests/DataAccessLayer.Tests/TransactionManagerTests.cs` |
| Savepoint SQL per provider | `tests/DataAccessLayer.Tests/Transactions/SavepointManagerTests.cs` |
| Suppressed scopes + advanced workflow | `tests/DataAccessLayer.Tests/Transactions/AdvancedTransactionWorkflowTests.cs` |
| `WithTransactionAsync` helper guarantees | `tests/DataAccessLayer.Tests/Transactions/TransactionManagerExtensionsTests.cs` |

Run `dotnet test tests/DataAccessLayer.Tests/DataAccessLayer.Tests.csproj --filter Category=Transactions` (or your organization’s test grouping) to execute the entire transaction suite after changes.

---

## 11. Reference map

| File | Purpose |
|------|---------|
| `Transactions/TransactionManager.cs` | Creates scopes, applies scope options, manages ambient stack. |
| `Transactions/TransactionScope.cs` | Commit/rollback logic, savepoints, resilience integration. |
| `Transactions/DependentTransactionScope.cs` | Behavior for nested `Required` scopes. |
| `Transactions/SuppressedTransactionScope.cs` | Connection-only scope semantics. |
| `Transactions/SavepointManager.cs` | Provider-specific savepoint SQL. |
| `Transactions/TransactionManagerExtensions.cs` | `WithTransactionAsync` helper. |
| `Transactions/TransactionScopeAmbient.cs` | `AsyncLocal` stack used by DAL helpers. |
| `tests/DataAccessLayer.Tests/TransactionManagerTests.cs` | Scope-option and ambient-behavior tests. |

Keep this doc in sync whenever you add new transaction features so engineers always have an example-driven reference.
