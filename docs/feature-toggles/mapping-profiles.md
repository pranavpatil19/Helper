# Mapping Profiles Toggle

- **Default:** `DbHelperFeatureToggles.EnableMappingProfiles = true`, so the DAL registers `EnumMappingProfile`, `OracleBooleanMappingProfile`, `OracleDateTimeMappingProfile`, and provider-specific normalizers.
- **Disable (manual mapping only):**

```csharp
services.AddDataAccessLayer(
    options,
    configureToggles: toggles =>
    {
        toggles.EnableMappingProfiles = false;
    });
```

Use this when you want to supply your own `IMappingProfile` registrations or keep the mapper surface minimal. Re-enable by setting the flag back to `true`.
