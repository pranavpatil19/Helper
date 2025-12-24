# Provider Helper Toggle

- **Default:** `DbHelperFeatureToggles.EnableProviderHelpers = true`, so Oracle/PostgreSQL-specific helpers (parameter utilities, column normalizers) are wired automatically.
- **Disable:**

```csharp
services.AddDataAccessLayer(
    options,
    configureToggles: toggles =>
    {
        toggles.EnableProviderHelpers = false;
    });
```

Only use this when you prefer to supply custom helpers or you are targeting a single provider that doesn’t require the built-ins. Set the flag back to `true` to restore the default behavior.
