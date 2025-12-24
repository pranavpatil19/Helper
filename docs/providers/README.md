# Provider Guide

## Feature Index
- Directory map (`sql`, `oracle`, `postgres`)
- ADO.NET usage (sync/async, stored procedures, provider quirks)
- EF Core integration patterns (transactions, compiled queries, bulk)
- Where to start in docs/tests

---

Each provider folder contains:

| Provider | ADO.NET docs | EF Core docs | Tests |
| --- | --- | --- | --- |
| SQL Server | `docs/providers/sql/ado/overview.md`, `docs/providers/sql/ado/sql-server.md`, plus `ado/async.md` & `ado/sync.md` | `docs/providers/sql/ef/overview.md`, `docs/providers/sql/ef/sql-server.md`, plus `ef/async.md` & `ef/sync.md` | `tests/DataAccessLayer.Tests` (SqlServer-prefixed suites, bulk writer tests) |
| PostgreSQL | `docs/providers/postgres/ado/overview.md`, `ado/async.md`, `ado/sync.md` | `docs/providers/postgres/ef/overview.md`, `ef/async.md`, `ef/sync.md` | `tests/DataAccessLayer.Tests/Postgres*` |
| Oracle | `docs/providers/oracle/ado/overview.md`, `ado/async.md`, `ado/sync.md` | `docs/providers/oracle/ef/overview.md`, `ef/async.md`, `ef/sync.md` | `tests/DataAccessLayer.Tests/Oracle*` |

Use these alongside `docs/usage-guide.md` and `docs/mapping-guide.md` for cross-provider patterns.
