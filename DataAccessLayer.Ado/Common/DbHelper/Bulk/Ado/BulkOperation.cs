using System;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

/// <summary>
/// Represents an executable bulk request (mapping + options + mode).
/// </summary>
public sealed class BulkOperation<T>
{
    public BulkOperation(
        BulkMapping<T> mapping,
        BulkOperationMode mode = BulkOperationMode.Insert,
        BulkOptions? options = null)
    {
        Mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        Mode = mode;
        Options = options ?? new BulkOptions();
    }

    public BulkMapping<T> Mapping { get; }

    public BulkOperationMode Mode { get; }

    public BulkOptions Options { get; }
}
