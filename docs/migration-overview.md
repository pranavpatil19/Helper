# FINAL – UPDATED

## Feature Index
- Supported databases/routes and core goals
- Layered architecture + role responsibilities
- Verification checklist for helper stack
- Unified bulk helper overview

## 1. Purpose & Vision
This helper stack targets enterprise-scale data migrations that must safely shuttle millions of rows between heterogeneous databases while keeping orchestration code clean and testable.

### Supported Databases
| Role   | Supported Providers                  |
|--------|--------------------------------------|
| Source | SQL Server, Oracle                   |
| Destination | SQL Server, PostgreSQL         |

### Supported Migration Paths
| Source      | Destination | Supported |
|-------------|-------------|-----------|
| SQL Server  | SQL Server  | ✅ |
| SQL Server  | PostgreSQL  | ✅ |
| Oracle      | SQL Server  | ✅ |
| Oracle      | PostgreSQL  | ✅ |

### Main Goals
| Goal | Description |
|------|-------------|
| High Performance | Handles multi-million row transfers via bulk writers, streaming readers, and pooled commands. |
| Scalability | Same pipelines scale horizontally by reusing DAL factories and configuration profiles. |
| Avoid Code Duplication | Shared abstractions (`DataAccessLayer`, `Shared`) eliminate provider-specific branches in business code. |
| Safety | Source-staging patterns plus transaction/savepoint helpers allow rollback and restarts. |
| Simplicity | Layered architecture keeps orchestration code readable and testable. |
| Security | UI/runner never touches databases directly; only Core orchestrates DAL access. |
| Unit Testing | `tests/DataAccessLayer.Tests` and higher-level suites cover helpers, mappers, transactions, and bulk flows. |

## 2. ✅ Source & Destination Behaviour
| Role | Operation Type |
|------|----------------|
| Source Business Tables | ✅ Read only |
| Source Staging Tables  | ✅ Read + Write |
| Destination Business Tables | ✅ Read + Write |
| Destination Staging Tables (optional) | ✅ Read + Write |
| UI Layer | ❌ No DB access |
| Core Layer | ✅ Controls all DB actions |

**Source DB**
- Business schemas: read-only usage enforced by consuming services.
- Staging schemas: read/write to support retry checkpoints.

**Destination DB**
- All required schemas permit read/write so migrations can upsert, validate, and reconcile.

This split enables:
- ✅ Restartable migrations (state stored in staging tables).
- ✅ Pre/post migration validation.
- ✅ Idempotent replay (merge/upsert semantics).
- ✅ Auditing via transaction logs + Serilog integration.

## 3. Layered Architecture
| Layer | Name | Responsibility | Implementation Reference |
|-------|------|----------------|--------------------------|
| 1 | Shared Library | Logging, configuration models, helpers (`Shared/Configuration`) | `Shared` project |
| 2 | Data Access Layer | All provider access, parameter binding, bulk/transaction helpers | `DataAccessLayer` project |
| 3 | Core Library | Business orchestration, migration workflows | `CoreBusiness` project |
| 4 | UI / Runner | Schedules migrations, never talks to DB directly | `MigrationRunner` (console/worker) |

**Golden Rule:** `UI → Core → DAL → Database`  
The solution already adheres to this chain: UI projects reference only the Core library, Core depends on the DAL, and the DAL is the sole layer that references provider packages (SqlClient, Npgsql, ODP.NET). Configuration (timeouts, credentials, retry policies) flows downward through `Shared.Configuration.DatabaseOptions`, ensuring consistent behavior across ADO.NET helpers and EF Core contexts.

## 4. Verification Checklist
- ✅ Cross-provider DAL abstractions (`IDatabaseHelper`, `ITransactionManager`) match the migration requirements.
- ✅ Bulk paths (SqlBulkCopy, PostgreSQL COPY, Oracle array bind) cover the “High Performance” goal.
- ✅ Staging-table strategy documented in `docs/transactions.txt` and enforced via DAL-only database access.
- ✅ Extensive tests (`tests/DataAccessLayer.Tests`, `tests/DataAccessLayer.Tests/Integration`) confirm parameter binding, transactions, bulk flows, and provider behaviors.
- ✅ Benchmark harness (`Data.Benchmarks`) ensures performance regressions are visible before release.

This document should be treated as the high-level contract for future migration features: any new work must preserve the layered structure, respect the read/write rules per role, and extend the DAL helpers instead of bypassing them.

## 5. Unified Bulk Helper

| Component | Description |
|-----------|-------------|
| `BulkMapping<T>` | Declarative table/column metadata + value projection (`new BulkColumn("customer_id"), row => new object?[] { row.Id, ... }`). |
| `BulkOperation<T>` | Wraps a mapping, desired `BulkOperationMode` (Insert/Merge/Update), and `BulkOptions` (keep identity, table lock, batch size, provider overrides). |
| `IBulkWriteHelper` | Single entry point that fans out to SqlBulkCopy, PostgreSQL COPY, or Oracle array binding based on `DatabaseOptions.Provider`. |
| Provider engines | Reuse existing writers (`SqlServerBulkWriter`, `PostgresBulkWriter`, `OracleBulkWriter`), so telemetry, retries, and connection management stay inside the DAL. |
| Transaction support | When an `ITransactionScope` is ambient, the helper reuses its open connection/transaction. If the scope rolls back, the bulk work rolls back with it. |

Usage pattern:

```csharp
public sealed class CustomerBulkMap : BulkMapping<Customer>
{
    public CustomerBulkMap()
        : base(
            "sales.customers",
            new[]
            {
                new BulkColumn("customer_id", isKey: true, isIdentity: true),
                new BulkColumn("full_name"),
                new BulkColumn("email", isNullable: true)
            },
            c => new object?[] { c.Id, c.Name, c.Email })
    {
    }
}

var helper = scope.ServiceProvider.GetRequiredService<IBulkWriteHelper>();
var operation = new BulkOperation<Customer>(
    new CustomerBulkMap(),
    BulkOperationMode.Insert,
    new BulkOptions { KeepIdentity = false, UseTableLock = true });

await helper.ExecuteAsync(operation, customers, cancellationToken);
```

Benefits:
- ✅ Same API for SQL Server, PostgreSQL, and Oracle.
- ✅ Mapping metadata lives in one place, keeping orchestration code clean.
- ✅ Options (identity retention, locking, batch size) are declarative and provider-aware.
- ✅ Integrated telemetry (Activity + ILogger) and FluentValidation ensure bad rows are caught before hitting the provider.
- ✅ Works inside `ITransactionScope`, so the migration’s “all or nothing” guarantee applies to bulk inserts too.
- ✅ Equally happy without a scope: if you don’t begin a transaction, the helper opens its own connection and relies on auto-commit. Use this for staging loads that don’t need atomicity.

### Provider-specific examples

**SQL Server (SqlBulkCopy)**
```csharp
var map = new SqlCustomerBulkMap();
var operation = new BulkOperation<Customer>(
    map,
    BulkOperationMode.Insert,
    new BulkOptions { UseTableLock = true });

await using var scope = await transactionManager.BeginAsync();
await bulkHelper.ExecuteAsync(operation, customers, cancellationToken);
await scope.CommitAsync(cancellationToken); // automatically rolls back on failure
```

**PostgreSQL (COPY BINARY)**
```csharp
var map = new PgInvoiceBulkMap();
var op = new BulkOperation<Invoice>(map);
await bulkHelper.ExecuteAsync(op, invoices, cancellationToken);
```

**Oracle (array binding)**
```csharp
var map = new OracleAuditBulkMap();
var op = new BulkOperation<AuditRow>(map, BulkOperationMode.Insert,
    new BulkOptions { BatchSize = 512 });

await using var scope = await transactionManager.BeginAsync(scopeOption: TransactionScopeOption.RequiresNew);
await bulkHelper.ExecuteAsync(op, auditRows, cancellationToken);
await scope.CommitAsync(cancellationToken);
```

### Test Coverage

| Test Suite | Provider Behavior Verified |
|------------|---------------------------|
| `SqlServerBulkWriterTests` + `Integration/SqlServerBulkWriterTests` | Column mappings, batching, and SqlBulkCopy plumbing |
| `PostgresBulkWriterTests` | COPY command generation, identifier quoting, streaming writes |
| `OracleBulkWriterTests` | Array-binding batches, DbType hints, value selectors |
| `BulkWriteHelperTests` | Oracle insert path and ambient transaction reuse |

When adding new bulk features or provider modes (e.g., Merge/Update), place tests in these suites so every scenario remains regression-tested.
