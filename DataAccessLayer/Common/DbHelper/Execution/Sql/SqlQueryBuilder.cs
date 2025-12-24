using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace DataAccessLayer.Execution;

/// <summary>
/// Fluent helper for composing parameterised SQL text without resorting to string interpolation.
/// </summary>
public sealed class SqlQueryBuilder
{
    private readonly string _from;
    private readonly List<string> _select = new();
    private readonly List<string> _where = new();
    private readonly List<string> _orderBy = new();
    private readonly List<DbParameterDefinition> _parameters = new();
    private CommandBehavior _commandBehavior = CommandBehavior.Default;
    private string? _traceName;

    private SqlQueryBuilder(string tableOrView, IEnumerable<string>? columns)
    {
        if (string.IsNullOrWhiteSpace(tableOrView))
        {
            throw new ArgumentException("Table or view name must be provided.", nameof(tableOrView));
        }

        _from = tableOrView;
        if (columns is not null)
        {
            foreach (var column in columns)
            {
                AddSelect(column);
            }
        }
    }

    /// <summary>
    /// Creates a builder targeting the supplied table or view.
    /// </summary>
    public static SqlQueryBuilder SelectFrom(string tableOrView, params string[] columns) =>
        new(tableOrView, columns);

    /// <summary>
    /// Adds the specified column to the SELECT projection.
    /// </summary>
    public SqlQueryBuilder AddSelect(string column)
    {
        if (string.IsNullOrWhiteSpace(column))
        {
            throw new ArgumentException("Column name cannot be empty.", nameof(column));
        }

        if (!_select.Contains(column, StringComparer.OrdinalIgnoreCase))
        {
            _select.Add(column);
        }

        return this;
    }

    /// <summary>
    /// Adds an equality filter (column = @pN). Null values are ignored when <paramref name="ignoreNull"/> is <c>true</c>.
    /// </summary>
    public SqlQueryBuilder WhereEquals(string column, object? value, DbType? dbType = null, bool ignoreNull = true)
    {
        if (ignoreNull && value is null)
        {
            return this;
        }

        var parameterName = AddScalarParameter(value, dbType, isNullable: true);
        _where.Add($"{column} = @{parameterName}");
        return this;
    }

    /// <summary>
    /// Adds a LIKE filter (column LIKE @pN).
    /// </summary>
    public SqlQueryBuilder WhereLike(string column, string pattern, bool ignoreNullOrEmpty = true)
    {
        if (ignoreNullOrEmpty && string.IsNullOrWhiteSpace(pattern))
        {
            return this;
        }

        var parameterName = AddScalarParameter(pattern, DbType.String, isNullable: true);
        _where.Add($"{column} LIKE @{parameterName}");
        return this;
    }

    /// <summary>
    /// Adds an IN filter (column IN (@pN)). Values are parameterised using TreatAsList to avoid SQL injection.
    /// </summary>
    public SqlQueryBuilder WhereIn(string column, IEnumerable<object?> values, DbType? dbType = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        var materialized = values as object?[] ?? values.ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("IN filter requires at least one value.", nameof(values));
        }

        var parameterName = AddListParameter(materialized, dbType);
        _where.Add($"{column} IN (@{parameterName})");
        return this;
    }

    /// <summary>
    /// Adds a raw filter expression. Use sparingly; prefer the strongly-typed helpers above.
    /// </summary>
    public SqlQueryBuilder WhereRaw(string condition, DbParameterDefinition? parameter = null)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            throw new ArgumentException("Condition cannot be empty.", nameof(condition));
        }

        _where.Add(condition);
        if (parameter is not null)
        {
            _parameters.Add(parameter);
        }

        return this;
    }

    /// <summary>
    /// Adds an ORDER BY clause.
    /// </summary>
    public SqlQueryBuilder OrderBy(string expression, bool descending = false)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Order by expression cannot be empty.", nameof(expression));
        }

        _orderBy.Add(descending ? $"{expression} DESC" : expression);
        return this;
    }

    /// <summary>
    /// Assigns a trace name propagated to <see cref="DbCommandRequest.TraceName"/>.
    /// </summary>
    public SqlQueryBuilder WithTraceName(string traceName)
    {
        _traceName = traceName;
        return this;
    }

    /// <summary>
    /// Sets the command behavior used when executing the generated request.
    /// </summary>
    public SqlQueryBuilder WithCommandBehavior(CommandBehavior behavior)
    {
        _commandBehavior = behavior;
        return this;
    }

    /// <summary>
    /// Builds the immutable <see cref="DbCommandRequest"/>.
    /// </summary>
    public DbCommandRequest Build()
    {
        var builder = new StringBuilder();
        builder.Append("SELECT ");
        builder.Append(_select.Count > 0 ? string.Join(", ", _select) : "*");
        builder.Append(" FROM ");
        builder.Append(_from);

        if (_where.Count > 0)
        {
            builder.Append(" WHERE ");
            builder.Append(string.Join(" AND ", _where));
        }

        if (_orderBy.Count > 0)
        {
            builder.Append(" ORDER BY ");
            builder.Append(string.Join(", ", _orderBy));
        }

        return new DbCommandRequest
        {
            CommandText = builder.ToString(),
            CommandType = CommandType.Text,
            Parameters = _parameters.ToArray(),
            TraceName = _traceName,
            CommandBehavior = _commandBehavior
        };
    }

    private string AddScalarParameter(object? value, DbType? dbType, bool isNullable)
    {
        var name = $"p{_parameters.Count}";
        _parameters.Add(new DbParameterDefinition
        {
            Name = name,
            Value = value,
            DbType = dbType,
            IsNullable = isNullable
        });

        return name;
    }

    private string AddListParameter(IReadOnlyList<object?> values, DbType? dbType)
    {
        var name = $"p{_parameters.Count}";
        _parameters.Add(new DbParameterDefinition
        {
            Name = name,
            TreatAsList = true,
            Values = values,
            DbType = dbType,
            IsNullable = true
        });

        return name;
    }
}
