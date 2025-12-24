namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Describes the logical intent of a bulk operation.
/// </summary>
public enum BulkOperationMode
{
    Insert = 0,
    Update = 1,
    Merge = 2
}
