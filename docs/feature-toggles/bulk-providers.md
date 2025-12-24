# Bulk Provider Whitelist

- **Default:** all provider engines are registered.
- **Whitelist specific providers:** edit `DalFeatureDefaults.CreateDefaultFeatures` and set `EnabledBulkProviders` to the set you want.

```csharp
private static DalFeatures CreateDefaultFeatures(DatabaseOptions options) =>
    DalFeatures.Default with
    {
        EnabledBulkProviders = new HashSet<DatabaseProvider>
        {
            DatabaseProvider.SqlServer,
            DatabaseProvider.PostgreSql
        }
    };
```

When the set is empty (`null`), the DAL registers every engine. When populated, only the listed providers stay active. Combine this with `BulkEngines = false` if you need to disable all engines entirely.
