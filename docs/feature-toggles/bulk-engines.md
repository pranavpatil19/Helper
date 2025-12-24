# Bulk Engine Toggle

- **Default:** bulk engines are enabled, so SQL Server/PostgreSQL/Oracle writers register automatically.
- **Disable globally (no `IBulkWriteHelper` and no EF bulk extensions):** edit `DalFeatureDefaults.CreateDefaultFeatures` and return `DalFeatures.Default with { BulkEngines = false }`.

```csharp
private static DalFeatures CreateDefaultFeatures(DatabaseOptions options) =>
    DalFeatures.Default with { BulkEngines = false };
```

- **Re-enable:** set `BulkEngines` back to `true`. Use this when you need to trim the DAL surface for lightweight services that never perform bulk operations.

> The EF bulk extensions (`WriteSqlServerBulkAsync`, `WritePostgresBulkAsync`, etc.) call the same engines under the covers. Leaving this flag on keeps bulk available to both ADO and EF entry points; turning it off removes bulk support everywhere.
