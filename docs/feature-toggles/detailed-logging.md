# Detailed Logging Toggle

- **Default:** detailed logs are off to keep production output lean.
- **Enable for troubleshooting:** edit `DalFeatureDefaults.CreateDefaultFeatures` so the manifest sets `DetailedLogging = true`.

```csharp
private static DalFeatures CreateDefaultFeatures(DatabaseOptions options) =>
    DalFeatures.Default with { DetailedLogging = true };
```

- **Disable again:** set the flag back to `false`. This primarily affects verbose command logging (parameter dumps, timings) that the DAL writes through `ILogger`.
