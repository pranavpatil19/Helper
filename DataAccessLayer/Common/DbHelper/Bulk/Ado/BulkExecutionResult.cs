namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Represents the outcome of a bulk write.
/// </summary>
public sealed class BulkExecutionResult
{
    public static BulkExecutionResult Empty { get; } = new(0, 0, 0);

    public BulkExecutionResult(int rowsInserted, int rowsUpdated, int rowsMerged)
    {
        RowsInserted = rowsInserted;
        RowsUpdated = rowsUpdated;
        RowsMerged = rowsMerged;
    }

    public int RowsInserted { get; }

    public int RowsUpdated { get; }

    public int RowsMerged { get; }

    public bool HasActivity => RowsInserted + RowsUpdated + RowsMerged > 0;
}
