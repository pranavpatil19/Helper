using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using DataAccessLayer.Execution;
using Shared.Configuration;

namespace DataAccessLayer.Providers.Postgres;

internal sealed class PostgresOutParameterPlan
{
    private readonly string? _scalarColumn;
    private readonly string[] _outputColumns;

    private PostgresOutParameterPlan(
        DbCommandRequest request,
        string[] outputColumns,
        string? scalarColumn)
    {
        Request = request;
        _outputColumns = outputColumns;
        _scalarColumn = scalarColumn;
    }

    public DbCommandRequest Request { get; }

    public static bool TryCreate(
        DbCommandRequest request,
        DatabaseProvider provider,
        out PostgresOutParameterPlan? plan)
    {
        plan = null;
        if (provider != DatabaseProvider.PostgreSql || request.Parameters.Count == 0)
        {
            return false;
        }

        var outputs = new List<string>();
        var rewrittenParameters = new List<DbParameterDefinition>(request.Parameters.Count);

        foreach (var parameter in request.Parameters)
        {
            switch (parameter.Direction)
            {
                case ParameterDirection.Input:
                    rewrittenParameters.Add(parameter);
                    break;
                case ParameterDirection.InputOutput:
                    outputs.Add(parameter.Name);
                    rewrittenParameters.Add(CloneWithDirection(parameter, ParameterDirection.Input));
                    break;
                case ParameterDirection.Output:
                case ParameterDirection.ReturnValue:
                    outputs.Add(parameter.Name);
                    break;
            }
        }

        if (outputs.Count == 0)
        {
            return false;
        }

        var commandText = BuildSelectStatement(request.CommandText, rewrittenParameters);
        var rewrittenRequest = CloneRequest(request, commandText, rewrittenParameters);

        string? scalarColumn = null;
        if (outputs.Count == 1)
        {
            scalarColumn = outputs[0];
        }

        plan = new PostgresOutParameterPlan(rewrittenRequest, outputs.ToArray(), scalarColumn);
        return true;
    }

    public IReadOnlyDictionary<string, object?> ReadOutputs(DbDataReader reader)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in _outputColumns)
        {
            var ordinal = GetOrdinal(reader, name);
            dict[name] = ordinal >= 0 && !reader.IsDBNull(ordinal)
                ? reader.GetValue(ordinal)
                : null;
        }

        return dict;
    }

    public object? GetScalar(DbDataReader reader)
    {
        if (_scalarColumn is not null)
        {
            var ordinal = GetOrdinal(reader, _scalarColumn);
            if (ordinal >= 0)
            {
                return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
            }
        }

        if (reader.FieldCount > 0)
        {
            return reader.IsDBNull(0) ? null : reader.GetValue(0);
        }

        return null;
    }

    private static DbCommandRequest CloneRequest(
        DbCommandRequest source,
        string commandText,
        IReadOnlyList<DbParameterDefinition> parameters)
    {
        return new DbCommandRequest
        {
            CommandText = commandText,
            CommandType = CommandType.Text,
            Parameters = parameters,
            CommandTimeoutSeconds = source.CommandTimeoutSeconds,
            PrepareCommand = source.PrepareCommand,
            Connection = source.Connection,
            CloseConnection = source.CloseConnection,
            Transaction = source.Transaction,
            OverrideOptions = source.OverrideOptions,
            CommandBehavior = CommandBehavior.SingleRow,
            TraceName = source.TraceName
        };
    }

    private static string BuildSelectStatement(string procedureName, IReadOnlyList<DbParameterDefinition> parameters)
    {
        var args = new List<string>(parameters.Count);
        foreach (var parameter in parameters)
        {
            args.Add("@" + parameter.Name);
        }

        var argumentList = string.Join(", ", args);
        return args.Count == 0
            ? $"select * from {procedureName}();"
            : $"select * from {procedureName}({argumentList});";
    }

    private static DbParameterDefinition CloneWithDirection(DbParameterDefinition source, ParameterDirection direction)
    {
        return new DbParameterDefinition
        {
            Name = source.Name,
            Value = source.Value,
            DbType = source.DbType,
            Direction = direction,
            Size = source.Size,
            Precision = source.Precision,
            Scale = source.Scale,
            IsNullable = source.IsNullable,
            DefaultValue = source.DefaultValue,
            ProviderTypeName = source.ProviderTypeName,
            TreatAsList = source.TreatAsList,
            Values = source.Values,
            ValueConverter = source.ValueConverter
        };
    }

    private static int GetOrdinal(DbDataReader reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
