# Transaction Helper Toggle

- **Default:** the DAL registers `ITransactionManager` + `ISavepointManager` so you can create ambient scopes/savepoints.
- **Disable transaction infrastructure:** edit `DalFeatureDefaults.CreateDefaultFeatures` and return a manifest with `Transactions = false`.

```csharp
private static DalFeatures CreateDefaultFeatures(DatabaseOptions options)
{
    return DalFeatures.Default with { Transactions = false };
}
```

With transactions disabled the helper still runs, but `ITransactionManager` is not registered and ambient scopes are unavailable. Re-enable by flipping the flag back to `true`.

> Multi-database coordination has been archived (see `Archive/MultiDbTransactions/`). Restore it only if your deployment requires the old behavior.
