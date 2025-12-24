using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using DataAccessLayer.Execution;

namespace DataAccessLayer.Providers.SqlServer;

/// <summary>
/// Helpers for building DataTables and TVP parameters for SQL Server.
/// </summary>
public static class SqlServerTvpBuilder
{
    /// <summary>
    /// Builds a <see cref="DataTable"/> for the specified rows and column selectors.
    /// </summary>
    public static DataTable ToDataTable<T>(IEnumerable<T> rows, params Expression<Func<T, object?>>[] columns)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (columns is null || columns.Length == 0)
        {
            throw new ArgumentException("At least one column selector must be provided.", nameof(columns));
        }

        var table = new DataTable();
        var compiled = new (string Name, Func<T, object?> Getter)[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            var expression = columns[i] ?? throw new ArgumentNullException(nameof(columns));
            var memberName = GetMemberName(expression.Body) ?? $"Column{i}";
            table.Columns.Add(memberName, typeof(object));
            compiled[i] = (memberName, expression.Compile());
        }

        foreach (var row in rows)
        {
            var values = new object?[compiled.Length];
            for (var i = 0; i < compiled.Length; i++)
            {
                values[i] = compiled[i].Getter(row) ?? DBNull.Value;
            }

            table.Rows.Add(values);
        }

        return table;
    }

    /// <summary>
    /// Creates a TVP parameter definition using the supplied rows and user-defined table type name.
    /// </summary>
    public static DbParameterDefinition CreateParameter<T>(
        string name,
        string typeName,
        IEnumerable<T> rows,
        params Expression<Func<T, object?>>[] columns)
    {
        var table = ToDataTable(rows, columns);
        return StructuredParameterBuilder.SqlServerTableValuedParameter(name, table, typeName);
    }

    private static string? GetMemberName(Expression expression) => expression switch
    {
        MemberExpression member => member.Member.Name,
        UnaryExpression { Operand: MemberExpression member } => member.Member.Name,
        _ => null
    };
}
