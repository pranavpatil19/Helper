# Migration Runner Configuration & DI Wiring

## Feature Index
- JSON schema for `MigrationRunnerOptions` (Source/Destination/Bulk/Logging)
- DI helper (`AddMigrationEndpoints`) and option sharing
- Accessing endpoint runtime options inside services
- Bulk/logging toggle guidance

The `MigrationRunner` host reads all of its settings from `appsettings.json` (or any bound configuration provider) using the strongly-typed `MigrationRunnerOptions` model.

```json
{
  "MigrationRunner": {
    "Source": {
      "Provider": "SqlServer",
      "ConnectionString": "Server=localhost;Database=HelperDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True",
      "Port": null,
      "EnableDetailedErrors": true,
      "EnableSensitiveDataLogging": false,
      "WrapProviderExceptions": true,
      "CommandTimeoutSeconds": 60,
      "ConnectionTimeoutSeconds": 15,
      "Resilience": {
        "EnableCommandRetries": true,
        "CommandRetryCount": 3,
        "CommandBaseDelayMilliseconds": 100,
        "EnableTransactionRetries": true,
        "TransactionRetryCount": 2,
        "TransactionBaseDelayMilliseconds": 250
      }
    },
    "Destination": {
      "Provider": "PostgreSql",
      "ConnectionString": "Host=localhost;Database=HelperDb;Username=postgres;Password=postgres",
      "Port": 5432,
      "EnableDetailedErrors": true,
      "EnableSensitiveDataLogging": false,
      "WrapProviderExceptions": true,
      "CommandTimeoutSeconds": 60,
      "ConnectionTimeoutSeconds": 15,
      "Resilience": {
        "EnableCommandRetries": true,
        "CommandRetryCount": 3,
        "CommandBaseDelayMilliseconds": 100,
        "EnableTransactionRetries": true,
        "TransactionRetryCount": 2,
        "TransactionBaseDelayMilliseconds": 250
      }
    },
    "Logging": {
      "LogPath": null
    },
    "Validation": {
      "Enabled": true,
      "DefaultRuleSets": null
    }
  }
}
```

- `Resilience` mirrors the DAL defaults (retries enabled, 3/2 attempts, exponential backoff). Override the booleans in JSON when you want to disable retries for a specific endpoint (e.g., local development) without touching code.

## How services are registered

`Program.cs` binds the options once:

```csharp
var runnerSection = builder.Configuration.GetSection(MigrationRunnerOptions.SectionName);
builder.Services.Configure<MigrationRunnerOptions>(runnerSection);
var options = runnerSection.Get<MigrationRunnerOptions>()
    ?? throw new DalConfigurationException("Missing MigrationRunner configuration.");
```

The helper extensions in `MigrationRunner/ServiceCollectionExtensions.cs` keep DI wiring simple:

```csharp
var sourceRegistration = EndpointRegistration.FromOptions(options.Source!);
var destinationRegistration = EndpointRegistration.FromOptions(
    options.Destination!,
    includeEntityFramework: false); // ADO-only destination example

builder.Services.AddMigrationEndpoints(sourceRegistration, destinationRegistration);
```

`EndpointRegistration` wraps the database options, optional validation callback, and optional DbContext customization for each endpoint. `AddMigrationEndpoints` takes the two registrations, wires the DAL + CoreBusiness (and EF if enabled), and records provider metadata inside `EndpointRuntimeOptions`. Skip EF by setting `IncludeEntityFramework = false` when building the registration for an endpoint that only needs the ADO surface.

Each method:
1. Converts the endpoint options into a `DatabaseOptions` instance (`DatabaseEndpointOptionsExtensions.ToDatabaseOptions`), including the optional `Port`.
2. Calls `services.AddDataAccessLayer(databaseOptions, ...)` so the DAL is configured for that provider/connection string (SqlServer, Oracle, PostgreSQL, etc.).
3. Calls `services.AddEcmEntityFrameworkSupport(databaseOptions)` to opt into the provider-specific EF Core contexts and repositories.
4. Calls `services.AddCoreBusiness()` so the normal business services/validators are available for that endpoint. Pass your own lambda (or use the root `MigrationRunner:Validation` settings) to enable/disable validation globally—or set `DefaultRuleSets` to force a rule set (for example `"Strict"`) to run for every validation call in the migration host. The runner automatically forwards both `Enabled` and `DefaultRuleSets` to CoreBusiness for source and destination.

> ✅ Verified by `MigrationRunner.Tests.ServiceRegistrationTests`: endpoint registrations resolve the provider-specific `IEcmDbContextFactory` without touching a real database connection.

This means DI automatically exposes two independent service graphs (one for the source DB, one for the destination DB) purely based on the JSON configuration—no dynamic factories required.

### Accessing endpoint settings inside services

Any service (Core or DAL consumers) can inject `IOptionsMonitor<EndpointRuntimeOptions>` and request the source/destination context:

```csharp
public sealed class MigrationJobService(IOptionsMonitor<EndpointRuntimeOptions> endpointOptions)
{
    public string SourceConnectionString => endpointOptions.GetSourceOptions().Database.ConnectionString;
    public string DestinationConnectionString => endpointOptions.GetDestinationOptions().Database.ConnectionString;
}
```

That exposes the connection string, command timeout (via `Database.CommandTimeoutSeconds`), and nullable bulk batch size exactly as defined in the JSON.

### Endpoint DbContext factories

- `AddSourceDbContextFactory` / `AddDestinationDbContextFactory` create per-endpoint `IEcmDbContextFactory` instances that honor the JSON options (provider, connection string, timeouts, diagnostics). Both can coexist even when the source and destination share the same provider because each factory builds its own DbContext options from the endpoint configuration.
- `GetSourceOptions()` / `GetDestinationOptions()` extension helpers wrap the named option lookup to avoid magic strings and keep orchestration code terse.

## Logging toggle

`Logging.LogPath` defaults to `null`. When you need a file sink, configure `LoggerFactory` (Serilog, FileLogger, etc.) in `Program.cs` using this value.

## Next steps

- Use `IOptions<MigrationRunnerOptions>` inside hosted services to inspect source/destination metadata for orchestration logic (timeouts, provider types, etc.).
- If you need EF contexts per endpoint, wrap `AddDbContextFactory` calls in similar extension helpers so they stay JSON-driven as well.

---

## Example: syncing user profiles between providers

The runner now ships with an end-to-end sample that copies rows from a *source* `Users` table into the *destination* `Users` table:

1. **Shared data contract**  
   `Shared/Entities/UserProfile.cs` defines the columns (Id/UserName/Email/IsActive/CreatedUtc/LastUpdatedUtc) and `DataAccessLayer.Database.ECM.Models.Configurations.UserProfileConfiguration` maps it to the `Users` table for every provider. `EcmDbContextBase` exposes the `DbSet<UserProfile>` so both SQL Server and Oracle/PostgreSQL migrations reuse the same configuration.

2. **Endpoint-aware gateways**  
   `MigrationRunner/Infrastructure/UserDataGateway.cs` implements `ISourceUserDataGateway` and `IDestinationUserDataGateway`. Each gateway is created with the corresponding `ISourceDbContextFactory` or `IDestinationDbContextFactory`, so it automatically uses the correct provider/connection string without additional switches. The gateway exposes two methods:
   - `GetUsersAsync` → pulls the full set from the endpoint using `AsNoTracking`.
   - `UpsertAsync` → inserts or updates rows inside the destination context, logging how many rows were affected.

3. **Business logic without duplication**  
   `MigrationRunner/Infrastructure/UserSynchronizationService.cs` is the single place where source and destination meet. It calls the two gateways, applies the “clone + upsert” logic, and returns the number of affected rows. Because the synchronization steps work with the abstractions, there are no provider-specific `if/else` blocks and adding new business rules (filters, transforms) happens in one method.

4. **DI wiring**  
   `MigrationRunner/Program.cs` registers the gateways and the synchronization service alongside `AddMigrationEndpoints`. The helpers reuse the same endpoint registrations you already have; no additional configuration schema is needed. `MigrationHostedService` resolves `IUserSynchronizationService` and runs it right after the database migrations, so a single `dotnet run` performs schema updates plus data copy.

5. **Operations**  
   Run the host with the desired `MigrationRunner` section (each endpoint choosing its provider/connection string). The hosted service logs which provider each gateway hits and how many rows were inserted/updated. Because the synchronization pipeline relies on the DAL abstractions (`IEcmDbContextFactory`, `EndpointRuntimeOptions`), it will work regardless of whether you pair SQL Server → Oracle, Oracle → PostgreSQL, etc.—only the JSON configuration changes.

Use this sample as a template for your own domain objects: add a shared entity, implement a gateway that works over `IEcmDbContextFactory` (or `IDatabaseHelper` for ADO scenarios), centralize transforms in a service, and register source/destination instances via the endpoint helpers so the providers stay declarative.
