# Mapping Guide

## Feature Index
- Overview of mapper strategies (Reflection, IL, Source-gen, Dictionary, Dynamic)
- `RowMapperFactory` + `RowMapperRequest` usage (column maps, casing, strategy overrides)
- Provider-specific conversions (Oracle NUMBER, PostgreSQL snake_case, SQL Server bit)
- Buffered projections (`DataTableMappingExtensions`, MemoryMarshal optimization)
- Streaming projections (`DbDataReaderMappingExtensions`, delegate mappers)
- Custom mapping delegates & troubleshooting tips
- Diagnostics, caching, and test references

---

## 1. Strategy Overview

| Strategy | Type Parameter | Notes | File |
| --- | --- | --- | --- |
| Reflection (`MapperStrategy.Reflection`) | Any class with setters | Flexible, supports column maps, respects `IgnoreCase`. | `DataAccessLayer/Common/DbHelper/Mapping/Strategies/ReflectionDataMapper.cs` |
| IL Emit (`MapperStrategy.IlEmit`) | Same as Reflection | Faster once warmed up; also supports column maps. | `.../IlEmitDataMapper.cs` |
| Source Generator (`MapperStrategy.SourceGenerator`) | Classes annotated with `[GeneratedMapper("MapperName")]` and compiled with `DataAccessLayer.SourceGenerators` | Zero reflection; build-time analyzer emits the mapper class in the consuming assembly. Keep `DataAccessLayer.SourceGenerators` referenced as an analyzer in `DataAccessLayer.csproj`. | `Mapping/GeneratedSamples/*`, analyzer: `DataAccessLayer.SourceGenerators/MapperGenerator.cs` |
| Dictionary (`MapperStrategy.Dictionary`) | `IReadOnlyDictionary<string, object?>` | Returns column/value pairs; respects `IgnoreCase`. | `.../DictionaryDataMapper.cs` |
| Dynamic (`MapperStrategy.Dynamic`) | `object` | Produces dynamic objects with property bags. | `.../DynamicDataMapper.cs` |

Strategy selection happens inside `RowMapperFactory` using `DbHelperOptions.DefaultMapperStrategy` by default. Per-query overrides use `RowMapperRequest.Strategy`.

---

## 2. RowMapperFactory & RowMapperRequest

```csharp
var mapperFactory = scope.ServiceProvider.GetRequiredService<IRowMapperFactory>();

var mapperRequest = new RowMapperRequest
{
    Strategy = MapperStrategy.Reflection,
    IgnoreCase = true,
    PropertyToColumnMap = new Dictionary<string, string>
    {
        [nameof(CustomerDto.Id)] = "CUSTOMER_ID",
        [nameof(CustomerDto.CreatedUtc)] = "CREATED_ON"
    }
};

var customers = await db.QueryAsync<CustomerDto>(request, mapperRequest);
```

- `PropertyToColumnMap` lets you alias provider-specific column names (Oracle uppercase, PostgreSQL snake_case) to DTO properties once per query.
- `IgnoreCase` overrides casing rules when column names differ only by case.
- Column maps are supported for Reflection and IL emit strategies (others throw `RowMappingException`).

---

## 3. Provider-Specific Conversions

The reflection mapper’s `PropertyAccessor.SetValue` calls `Convert.ChangeType` whenever the raw column type doesn’t match the property type, so common provider quirks are handled automatically:

- Oracle `NUMBER(1)` → `bool`
- SQL Server `bit` → `bool`
- Oracle/PostgreSQL timestamp strings → `DateTime`
- Numeric columns into enums (if the enum underlying type is numeric)

For non-standard encodings (e.g., Oracle storing `Y/N`, `CHAR(1)` flags), provide a delegate mapper:

```csharp
var customers = await db.QueryAsync(
    request,
    reader => new CustomerDto
    {
        Id = reader.GetInt32(0),
        IsPreferred = reader.GetString(3) == "Y",
        CreatedUtc = reader.GetDateTime(4).ToUniversalTime()
    });
```

You can reuse the same delegate with `DataTable` or `DbDataReader` projections by passing it directly to `MapRows`.

Tests covering these scenarios live in `tests/DataAccessLayer.Tests/Mapping/ReflectionDataMapperTests.cs` and `.../DataTableMappingExtensionsTests.cs`.

---

## 4. Buffered Projections (DataTable/DataSet)

`DataTableMappingExtensions` lets you convert buffered shapes into DTO lists without re-running the query:

```csharp
var tableResult = await db.LoadDataTableAsync(request);
var mapperFactory = scope.ServiceProvider.GetRequiredService<IRowMapperFactory>();

var customers = tableResult.Data.MapRows<CustomerDto>(mapperFactory, mapperRequest);

// Snake_case columns -> PascalCase DTO properties
var orders = tableResult.Data.MapRows<OrderDto>(
    mapperFactory,
    new RowMapperRequest
    {
        PropertyToColumnMap = new Dictionary<string, string>
        {
            [nameof(OrderDto.Quantity)] = "qty",
            [nameof(OrderDto.Total)] = "order_total"
        }
    });

// Dictionary projection for flexible consumers
var dictionaryRows = tableResult.Data.CreateDataReader().MapDictionaries(mapperFactory);
```

Performance considerations:
- The helper allocates a single `T[]` via `GC.AllocateUninitializedArray` and fills it through `MemoryMarshal.CreateSpan`, so there are no per-row allocations.
- Column maps and strategy overrides flow through `RowMapperRequest`, just like live queries.
- DataSet overloads let you pick tables by name or index.

### Provider-aware example (Oracle style result set)

```csharp
var oracleTable = new DataTable();
oracleTable.Columns.Add("CUSTOMER_ID", typeof(decimal));
oracleTable.Columns.Add("IS_PREFERRED", typeof(string));      // Y / N
oracleTable.Columns.Add("CREATED_ON", typeof(DateTime));      // Oracle DATE
oracleTable.Columns.Add("STATUS_CODE", typeof(string));       // Enum friendly
oracleTable.Rows.Add(10m, "Y", DateTime.SpecifyKind(new DateTime(2024, 1, 1, 8, 30, 0), DateTimeKind.Utc), "Active");
oracleTable.Rows.Add(11m, "N", DateTime.SpecifyKind(new DateTime(2024, 2, 1, 9, 0, 0), DateTimeKind.Utc), "Disabled");

var mapperFactory = new RowMapperFactory(
    new DbHelperOptions(),
    profiles: new IMappingProfile[]
    {
        new OracleBooleanMappingProfile(),
        new OracleDateTimeMappingProfile(),
        new EnumMappingProfile()
    });

var mapperRequest = new RowMapperRequest
{
    PropertyToColumnMap = new Dictionary<string, string>
    {
        [nameof(CustomerSnapshot.Id)] = "CUSTOMER_ID",
        [nameof(CustomerSnapshot.IsPreferred)] = "IS_PREFERRED",
        [nameof(CustomerSnapshot.CreatedUtc)] = "CREATED_ON",
        [nameof(CustomerSnapshot.Status)] = "STATUS_CODE"
    }
};

var customers = oracleTable.MapRows<CustomerSnapshot>(mapperFactory, mapperRequest);
```

```csharp
public sealed class CustomerSnapshot
{
    public int Id { get; set; }
    public bool IsPreferred { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public OrderStatus Status { get; set; }
}

public enum OrderStatus
{
    Pending,
    Active,
    Disabled
}
```

The mapper factory applies the Oracle profiles before falling back to `Convert.ChangeType`, so:
- `"Y" / "N"` strings become CLR booleans via `OracleBooleanMappingProfile`.
- Oracle `DATE/TIMESTAMP` values (surfaced as `DateTime`) become UTC `DateTimeOffset` via `OracleDateTimeMappingProfile`.
- `STATUS_CODE` strings map directly into an enum by `EnumMappingProfile`.

> In production these profiles are registered automatically when the DAL detects an Oracle provider, so you normally only pass `RowMapperRequest` with the column map. The sample above mirrors the automated coverage in `tests/DataAccessLayer.Tests/Mapping/DataTableMappingExtensionsTests.MapRows_WithProviderProfiles_ConvertsDatesEnumsAndBooleans`.

### Column name normalization (snake_case → PascalCase)

When PostgreSQL surfaces snake_case column names and you do not want to repeat `PropertyToColumnMap`, register an `IColumnNameNormalizer`. The DAL already wires `PostgresSnakeCaseColumnNameNormalizer` whenever `DatabaseOptions.Provider == PostgreSql`, so `order_id` automatically maps to `OrderId`.

```csharp
// inside Startup/Program
services.AddDataAccessLayer(databaseOptions);

// If you need a custom scheme (e.g., legacy naming), register your own normalizer:
services.AddSingleton<IColumnNameNormalizer, LegacyColumnNameNormalizer>();
```

With a normalizer in place, `RowMapperFactory` wraps every reader in a lightweight adapter that rewrites `GetName` results before handing them to the mapper. You can still mix in column maps for the one-off mismatches. See `tests/DataAccessLayer.Tests/Mapping/PostgresSnakeCaseColumnNameNormalizerTests.RowMapper_UsesNormalizer` for the regression test.

Tests: `tests/DataAccessLayer.Tests/Mapping/DataTableMappingExtensionsTests.cs`.

---

## 5. Streaming Projections (DbDataReader)

When you lease a reader via `ExecuteReaderAsync`, use `DbDataReaderMappingExtensions` to materialize the remaining rows:

```csharp
await using var lease = await db.ExecuteReaderAsync(request);
var mapperFactory = scope.ServiceProvider.GetRequiredService<IRowMapperFactory>();

var list = lease.Reader.MapRows<CustomerDto>(mapperFactory, mapperRequest);
var dictionaries = lease.Reader.MapDictionaries(mapperFactory);
var table = lease.Reader.ToDataTable("CustomersSnapshot");
```

Each helper consumes the current result set—call `NextResult` manually if you need to move to subsequent sets before mapping.

Tests: `tests/DataAccessLayer.Tests/Mapping/DbDataReaderMappingExtensionsTests.cs`.

---

## 6. Custom Delegates & Troubleshooting

- **Delegate mappers**: Pass `Func<DbDataReader, T>` to any `Query*`, `Stream*`, or extension method when you need full control (complex calculations, provider-specific logic).
- **Caching**: When `DbHelperOptions.EnableMapperCaching = true` (default), `RowMapperFactory` caches mappers by `(Type, Strategy, IgnoreCase)` to avoid repeated reflection/IL work.
- **Exceptions**: `RowMappingException` highlights unsupported scenarios (e.g., column maps with dictionary strategy, missing `[GeneratedMapper]` attribute). Wrap map calls in logging pipelines if you need extra context.
- **Diagnostics**: Use the integration tests as templates for verifying schema-to-DTO alignment; add coverage whenever you introduce new column maps or delegate mappers.

### Mapping Profiles (custom converters)

Register `IMappingProfile` implementations to centralize provider-specific conversions instead of sprinkling delegate mappers across calls:

```csharp
public sealed class OracleBooleanProfile : IMappingProfile
{
    public bool TryConvert(string columnName, string propertyName, Type targetType, object? sourceValue, out object? destinationValue)
    {
        if (targetType == typeof(bool) && sourceValue is string s && (s == "Y" || s == "N"))
        {
            destinationValue = s == "Y";
            return true;
        }

        destinationValue = null;
        return false;
    }
}

services.AddSingleton<IMappingProfile, OracleBooleanProfile>();
```

`RowMapperFactory` picks up all registered profiles and applies them before falling back to `Convert.ChangeType`, so the same converter works whether you call `QueryAsync`, `LoadDataTable`, or `DbDataReader.MapRows`.

Built-in profiles:
- `OracleBooleanMappingProfile` (registered automatically when `DatabaseOptions.Provider == Oracle`) converts `NUMBER(1)`, `CHAR(1)`, and `Y/N` flags into CLR `bool` values. You can add more profiles via DI to extend or override the defaults.
- `OracleDateTimeMappingProfile` converts Oracle `DATE`/`TIMESTAMP` values (including strings) into UTC `DateTimeOffset`/`DateTime` instances so DTOs see consistent timestamps.
- `EnumMappingProfile` (registered for all providers) converts numeric or string columns into enum DTO properties, so SQL Server/PostgreSQL status columns map cleanly to strongly-typed enums.

---

## 7. References

- Source: `DataAccessLayer/Common/DbHelper/Mapping/*`
- Tests: `tests/DataAccessLayer.Tests/Mapping/*`
- Docs: `docs/usage-guide.md` (section 2), `docs/feature-index.md`
