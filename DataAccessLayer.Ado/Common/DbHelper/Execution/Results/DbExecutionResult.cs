using System;
using System.Collections.Generic;

namespace DataAccessLayer.Execution;

/// <summary>
/// Represents metadata captured after executing a database command (rows, scalar, OUT parameters).
/// </summary>
/// <remarks>
/// Produced by the top-level methods on <see cref="IDatabaseHelper"/> (for example, <see cref="IDatabaseHelper.ExecuteAsync"/>,
/// <see cref="IDatabaseHelper.Execute"/>, <see cref="IDatabaseHelper.ExecuteScalarAsync"/>, and <see cref="IDatabaseHelper.ExecuteScalar"/>),
/// capturing rows affected, scalar payloads, and any OUT/RETURN parameters.
/// </remarks>
public sealed class DbExecutionResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DbExecutionResult"/> class.
    /// </summary>
    /// <param name="rowsAffected">The number of rows impacted according to the provider.</param>
    /// <param name="scalar">An optional scalar value returned by the command.</param>
    /// <param name="outputParameters">Materialized OUT/RETURN parameters.</param>
    public DbExecutionResult(int rowsAffected, object? scalar, IReadOnlyDictionary<string, object?> outputParameters)
    {
        OutputParameters = outputParameters ?? throw new ArgumentNullException(nameof(outputParameters));
        RowsAffected = rowsAffected;
        Scalar = scalar;
    }

    /// <summary>
    /// Gets the provider-reported rows affected count (may be -1 for statements where it is unknown).
    /// </summary>
    public int RowsAffected { get; }

    /// <summary>
    /// Gets the scalar value returned by the command when applicable.
    /// </summary>
    public object? Scalar { get; }

    /// <summary>
    /// Gets the OUT/RETURN parameters keyed by their logical names.
    /// </summary>
    public IReadOnlyDictionary<string, object?> OutputParameters { get; }

    /// <summary>
    /// Gets a value indicating whether any output data was collected.
    /// </summary>
    public bool HasOutputParameters => OutputParameters.Count > 0;
}
