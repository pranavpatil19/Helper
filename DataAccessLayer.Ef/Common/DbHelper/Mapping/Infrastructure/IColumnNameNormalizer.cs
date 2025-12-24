namespace DataAccessLayer.Mapping;

/// <summary>
/// Normalizes column names (e.g., snake_case to PascalCase) before matching properties.
/// </summary>
public interface IColumnNameNormalizer
{
    string Normalize(string columnName);
}
