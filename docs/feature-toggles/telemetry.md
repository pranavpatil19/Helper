# Telemetry Toggle

- **Default:** telemetry is enabled (activities + metrics emitted through `IDataAccessTelemetry`).
- **Disable:** edit `DalFeatureDefaults.CreateDefaultFeatures` so the manifest sets `Telemetry = false`.

```csharp
// DataAccessLayer/Common/DbHelper/Configuration/DalFeatureDefaults.cs
private static DalFeatures CreateDefaultFeatures(DatabaseOptions options)
{
    return DalFeatures.Default with { Telemetry = false };
}
```

When telemetry is disabled the DAL wires `NoOpDataAccessTelemetry`, so no OpenTelemetry spans or counters are produced until you turn it back on.
