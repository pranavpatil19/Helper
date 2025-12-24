using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using DataAccessLayer.Execution;

namespace DataAccessLayer.Providers.Postgres;

/// <summary>
/// Helpers that emulate REF CURSOR-style multi-result flows for PostgreSQL by wiring up DO blocks + FETCH statements.
/// </summary>
public static class PostgresCursorHelper
{
    /// <summary>
    /// Builds a <see cref="DbCommandRequest"/> that opens one cursor per SELECT statement and fetches them sequentially.
    /// The resulting request can be sent through <see cref="IDatabaseHelper"/> and consumed via <see cref="DbDataReader.NextResult"/>.
    /// </summary>
    /// <param name="selectStatements">One or more SELECT statements that will be opened as cursors.</param>
    /// <param name="parameters">Optional parameter definitions shared by all statements.</param>
    /// <param name="traceName">Optional label for logging/telemetry.</param>
    public static DbCommandRequest BuildMultiCursorRequest(
        IReadOnlyList<string> selectStatements,
        IReadOnlyList<DbParameterDefinition>? parameters = null,
        string? traceName = null)
    {
        if (selectStatements is null || selectStatements.Count == 0)
        {
            throw new ArgumentException("At least one SELECT statement must be provided.", nameof(selectStatements));
        }

        var commandText = ComposeCursorScript(selectStatements);
        return new DbCommandRequest
        {
            CommandText = commandText,
            CommandType = CommandType.Text,
            Parameters = parameters ?? Array.Empty<DbParameterDefinition>(),
            TraceName = traceName
        };
    }

    private static string ComposeCursorScript(IReadOnlyList<string> selectStatements)
    {
        var builder = new StringBuilder();
        builder.AppendLine("DO $$");
        builder.AppendLine("DECLARE");
        for (var i = 0; i < selectStatements.Count; i++)
        {
            builder.Append("    cursor_").Append(i).Append(" refcursor := 'cursor_").Append(i).AppendLine("';");
        }

        builder.AppendLine("BEGIN");
        for (var i = 0; i < selectStatements.Count; i++)
        {
            builder.Append("    OPEN cursor_").Append(i).Append(" FOR ").Append(selectStatements[i]).AppendLine(";");
        }

        builder.AppendLine("END $$;");
        for (var i = 0; i < selectStatements.Count; i++)
        {
            builder.Append("FETCH ALL FROM cursor_").Append(i).AppendLine(";");
        }

        return builder.ToString();
    }
}
