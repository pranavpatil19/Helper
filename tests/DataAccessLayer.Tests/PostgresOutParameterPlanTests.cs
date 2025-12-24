#nullable enable

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using DataAccessLayer.Execution;
using DataAccessLayer.Providers.Postgres;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class PostgresOutParameterPlanTests
{
    [Fact]
    public void TryCreate_ReturnsFalse_WhenNoOutputs()
    {
        var request = new DbCommandRequest
        {
            CommandText = "func_no_outputs",
            CommandType = CommandType.StoredProcedure,
            Parameters = new[]
            {
                DbParameterCollectionBuilder.Input("id", 1)
            }
        };

        var result = PostgresOutParameterPlan.TryCreate(request, DatabaseProvider.PostgreSql, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryCreate_RewritesCommandText_AndParameters()
    {
        var request = new DbCommandRequest
        {
            CommandText = "func_outputs",
            CommandType = CommandType.StoredProcedure,
            Parameters = new[]
            {
                DbParameterCollectionBuilder.Input("id", 42),
                new DbParameterDefinition { Name = "value_out", Direction = ParameterDirection.Output },
                new DbParameterDefinition { Name = "value_inout", Direction = ParameterDirection.InputOutput, Value = 5 }
            }
        };

        Assert.True(PostgresOutParameterPlan.TryCreate(request, DatabaseProvider.PostgreSql, out var plan));
        Assert.NotNull(plan);
        Assert.Equal("select * from func_outputs(@id, @value_inout);", plan!.Request.CommandText);
        Assert.All(plan.Request.Parameters, p => Assert.Equal(ParameterDirection.Input, p.Direction));
        Assert.Equal(CommandBehavior.SingleRow, plan.Request.CommandBehavior);
    }

    [Fact]
    public void ReadOutputs_ExtractsValues_ByColumnName()
    {
        var request = new DbCommandRequest
        {
            CommandText = "func_outputs",
            CommandType = CommandType.StoredProcedure,
            Parameters = new[]
            {
                new DbParameterDefinition { Name = "value_out", Direction = ParameterDirection.Output }
            }
        };

        Assert.True(PostgresOutParameterPlan.TryCreate(request, DatabaseProvider.PostgreSql, out var plan));
        var reader = new FakeReader(new Dictionary<string, object?>
        {
            ["value_out"] = 123
        });

        var outputs = plan!.ReadOutputs(reader);
        Assert.Equal(123, outputs["value_out"]);
    }

    private sealed class FakeReader : DbDataReader
    {
        private readonly Dictionary<string, object?> _values;
        private bool _read;

        public FakeReader(Dictionary<string, object?> values)
        {
            _values = values;
        }

        public override bool Read()
        {
            if (_read)
            {
                return false;
            }

            _read = true;
            return true;
        }

        public override int FieldCount => _values.Count;
        public override bool HasRows => _values.Count > 0;
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => _values[name]!;
        public override bool IsClosed => false;
        public override int RecordsAffected => 1;
        public override bool NextResult() => false;
        public override int Depth => 0;

        public override string GetName(int ordinal) => GetKey(ordinal);
        public override string GetDataTypeName(int ordinal) => GetValue(ordinal)?.GetType().Name ?? typeof(object).Name;
        public override Type GetFieldType(int ordinal) => GetValue(ordinal)?.GetType() ?? typeof(object);
        public override object GetValue(int ordinal) => _values[GetKey(ordinal)]!;
        public override int GetValues(object[] values)
        {
            var i = 0;
            foreach (var value in _values.Values)
            {
                if (i >= values.Length)
                {
                    break;
                }

                values[i++] = value!;
            }

            return i;
        }

        public override int GetOrdinal(string name)
        {
            var index = 0;
            foreach (var key in _values.Keys)
            {
                if (string.Equals(key, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        public override bool IsDBNull(int ordinal) => GetValue(ordinal) is null;

        public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
        public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal)!;
        public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
        public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
        public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
        public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
        public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
        public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal)) ?? string.Empty;
        public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
        public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
        public override System.Collections.IEnumerator GetEnumerator() => _values.Values.GetEnumerator();

        private string GetKey(int ordinal)
        {
            var index = 0;
            foreach (var key in _values.Keys)
            {
                if (index == ordinal)
                {
                    return key;
                }

                index++;
            }

            throw new IndexOutOfRangeException();
        }
    }
}
