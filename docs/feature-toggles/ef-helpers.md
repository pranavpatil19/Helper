# EF Helper Toggle

- **Default:** enabled (the DAL registers EF-specific helpers only when you call `AddEcmEntityFrameworkSupport`).
- **Packages required when enabled:** add the provider EF package that matches your DB (`Microsoft.EntityFrameworkCore.SqlServer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, or `Oracle.EntityFrameworkCore`) alongside the DAL project.
- **Skip EF registrations entirely:** either omit the helper call or edit `DalFeatureDefaults.CreateDefaultFeatures` so the manifest sets `EfHelpers = false` (the extension becomes a no-op).

```csharp
private static DalFeatures CreateDefaultFeatures(DatabaseOptions options) =>
    DalFeatures.Default with { EfHelpers = false };
```

- **Re-enable:** set `EfHelpers = true` before invoking `AddEcmEntityFrameworkSupport` so DbContexts, repositories, and `IMigrationService` are registered again.
