# Resilience

- **Default:** resilience is on, so the DAL registers `ResilienceStrategy` and honors every setting inside `DatabaseOptions.Resilience`.
- **Disable:** edit `DalFeatureDefaults.CreateDefaultFeatures` and set `Resilience = false`.

```csharp
private static DalFeatures CreateDefaultFeatures(DatabaseOptions options) =>
    DalFeatures.Default with { Resilience = false };
```

Disabling the flag swaps in `NoOpResilienceStrategy`, meaning `IDatabaseHelper`, transaction scopes, and EF retry helpers (`DbContextExtensions.SaveChangesWithRetry*`) run exactly once. This is handy for local debugging when you want the first exception instead of multiple attempts.

> Note: `DatabaseOptions.Resilience` still drives retry counts, delays, and enable flags whenever the feature is on. When you re-enable the module, the DAL immediately returns to the configured Polly behavior without further code changes.
