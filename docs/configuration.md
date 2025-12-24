# Configuration Quick Start

Keep the setup to three choices: declare the provider + connection string, decide whether the host needs the EF helpers, and (optionally) trim modules by editing one file. Nothing else is required at call sites.

---

## 1. Minimal `appsettings.json`

```json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=.;Database=Sample;Trusted_Connection=True;",
    "CommandTimeoutSeconds": 60,
    "ConnectionTimeoutSeconds": 15,
    "Resilience": { "EnableCommandRetries": true, "EnableTransactionRetries": true },
    "CommandPool": { "EnableCommandPooling": true, "EnableParameterPooling": true }
  }
}
```

Only add properties when you truly need them:
- `Diagnostics.LogEffectiveTimeouts` → log when the DAL rewrites timeouts.
- `ParameterBinding.*` / `InputNormalization.*` → trim strings, clamp dates, enforce decimal precision before commands run.
- `WrapProviderExceptions` → flip to `false` if raw provider errors are preferred during debugging.

All other settings are documented in `docs/usage-guide.md`; most apps can ignore them.

---

## 2. Register services (one or two lines)

```csharp
var options = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>()
    ?? throw new InvalidOperationException("Missing Database section.");

builder.Services.AddDataAccessLayer(options);              // Always call this.
builder.Services.AddEcmEntityFrameworkSupport(options);    // Only when EF helpers are needed.
```

- ADO-only workloads call the first line and stop. EF consumers call both lines so DbContexts/repositories are available. Validation, telemetry, resilience, transactions, and bulk helpers are automatically wired by `AddDataAccessLayer`; there is nothing else to toggle.
- Connection strings are normalized per provider (SQL Server/PostgreSQL/Oracle) inside `ConnectionStringFactory`, so you only supply the base string + timeouts once.
- Validation is not optional. To override the shared rules, replace or decorate the registered `IValidator<T>` implementation or inherit from `CoreBusiness.Validation.CommonValidationRulesBase<T>`.

---

### Source + destination connection strings

Need to hydrate both a source and a destination database (for migrations, sync jobs, etc.)? Keep the same JSON shape but under the `MigrationRunner` section so each endpoint has its own provider and connection profile. If you omit `ConnectionString`, the runner will build one automatically based on the provider-specific settings you supply:

```json
{
  "MigrationRunner": {
    "Source": {
      "Provider": "SqlServer",
      "CommandTimeoutSeconds": 60,
      "ConnectionTimeoutSeconds": 15,
      "SqlServer": {
        "Server": ".",
        "Database": "SourceDb",
        "TrustedConnection": true,
        "TrustServerCertificate": true
      }
    },
    "Destination": {
      "Provider": "Oracle",
      "CommandTimeoutSeconds": 60,
      "ConnectionTimeoutSeconds": 15,
      "Oracle": {
        "Host": "localhost",
        "Port": 1521,
        "ServiceName": "XEPDB1",
        "UserId": "dest",
        "Password": "secret"
      }
    },
    "Logging": { "LogPath": null }
  }
}
```

`MigrationRunner/Configuration/MigrationRunnerOptions.cs` already exposes `Source` and `Destination` properties (type `DatabaseEndpointOptions`) so DI can look like:

```csharp
var runnerOptions = builder.Configuration
    .GetSection(MigrationRunnerOptions.SectionName)
    .Get<MigrationRunnerOptions>()
    ?? throw new DalConfigurationException("Missing MigrationRunner configuration.");

var sourceRegistration = EndpointRegistration.FromOptions(runnerOptions.Source!);
var destinationRegistration = EndpointRegistration.FromOptions(runnerOptions.Destination!);

builder.Services.AddMigrationEndpoints(sourceRegistration, destinationRegistration);
```

Each endpoint gets its own provider settings, timeouts, and connection profile. When `ConnectionString` is omitted, `DatabaseEndpointOptionsExtensions` uses the provider-specific section (`SqlServer`, `Postgres`, or `Oracle`, defined in `Shared/Configuration/DatabaseConnectionProfileOptions.cs`) to build the string before wiring DI. You can still paste a raw connection string when you prefer—setting `ConnectionString` bypasses the builder entirely. Mixing providers (SQL Server → Oracle, PostgreSQL → SQL Server, etc.) is just a matter of updating those JSON sections; the helper classes reuse the same `DatabaseOptions` pipeline internally.

---

## 3. Optional trims (edit once, done everywhere)

If you want to permanently disable something (for example telemetry or bulk writers) edit `DataAccessLayer/Common/DbHelper/Configuration/DalFeatureDefaults.cs`. The default implementation returns `DalFeatures.Default`, which keeps every subsystem on. A quick tweak looks like this:

```csharp
private static DalFeatures CreateDefaultFeatures(DatabaseOptions options) =>
    options.Provider switch
    {
        DatabaseProvider.Oracle => DalFeatures.Default with { BulkEngines = false },
        _ => DalFeatures.Default
    };
```

Flags available inside `DalFeatures`:
- `Telemetry`, `DetailedLogging`
- `BulkEngines` + `EnabledBulkProviders`
- `Transactions`
- `EfHelpers`
- `Resilience`

Because the defaults are hard-coded, the DLL you ship already reflects the feature mix you want—no runtime toggles or feature manifests in DI calls.

---

## 4. Handy links when you need more

- Telemetry configuration → `docs/feature-toggles/telemetry.md`
- Bulk engines/provider matrix → `docs/bulk-operations.md`
- Transactions & savepoints → `docs/transactions.txt`
- Migration runner / dual-endpoint setup → `docs/migration-runner.md`
- Everything else (command/query samples, EF helpers, provider recipes) → `docs/usage-guide.md`

Remember: one JSON section, one (maybe two) service calls, and your edits to `DalFeatureDefaults` if you want to trim anything globally. Everything else (validation, mapping, telemetry wiring, bulk engines) ships ready to use.
