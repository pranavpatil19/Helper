using System;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;

namespace DataAccessLayer.Common.DbHelper;

public sealed partial class DatabaseHelper
{
    /// <summary>
    /// Executes an Oracle stored procedure that returns a REF CURSOR asynchronously.
    /// </summary>
    public async Task<DbReaderScope> ExecuteRefCursorAsync(
        DbCommandRequest request,
        string cursorParameterName,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(cursorParameterName);
        EnsureOracleProvider(request);
        try
        {
            return await ExecuteRefCursorCoreAsync(request, cursorParameterName, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw WrapException(request, ex);
        }
    }

    /// <summary>
    /// Executes an Oracle stored procedure that returns a REF CURSOR synchronously.
    /// </summary>
    public DbReaderScope ExecuteRefCursor(
        DbCommandRequest request,
        string cursorParameterName)
    {
        ValidateRequest(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(cursorParameterName);
        EnsureOracleProvider(request);
        try
        {
            return ExecuteRefCursorCore(request, cursorParameterName);
        }
        catch (Exception ex)
        {
            throw WrapException(request, ex);
        }
    }
}
