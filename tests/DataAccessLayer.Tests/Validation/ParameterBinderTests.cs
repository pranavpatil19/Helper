#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using DataAccessLayer.Execution;
using DalParameter = DataAccessLayer.Execution.Builders.DbParameter;
using DataAccessLayer.Common.DbHelper;
using Shared.Configuration;
using Xunit;

namespace DataAccessLayer.Tests.Validation;

public sealed class ParameterBinderTests
{
    [Fact]
    public void TrimStringsOption_TrimsValues()
    {
        var binder = new ParameterBinder(new PassThroughParameterPool(), new ParameterBindingOptions
        {
            TrimStrings = true
        });
        var command = new RecordingCommand();
        var parameters = new[]
        {
            DalParameter.Input("Name", "  alpha  ")
        };

        binder.BindParameters(command, parameters, DatabaseProvider.SqlServer);

        Assert.Single(command.RecordedParameters);
        Assert.Equal("alpha", command.RecordedParameters[0].Value);
    }

    [Fact]
    public void TrimStringsOption_AllowsEmptyStringToBecomeNull_WhenNullable()
    {
        var binder = new ParameterBinder(new PassThroughParameterPool(), new ParameterBindingOptions
        {
            TrimStrings = true
        });
        var command = new RecordingCommand();
        var parameters = new[]
        {
            new DbParameterDefinition
            {
                Name = "Name",
                Value = "   ",
                IsNullable = true
            }
        };

        binder.BindParameters(command, parameters, DatabaseProvider.SqlServer);

        Assert.Equal(DBNull.Value, command.RecordedParameters[0].Value);
    }

    [Fact]
    public void DefaultValue_ReplacesDefaultStructs()
    {
        var binder = new ParameterBinder(new PassThroughParameterPool());
        var command = new RecordingCommand();
        var parameters = new[]
        {
            new DbParameterDefinition
            {
                Name = "Amount",
                Value = 0m,
                DefaultValue = 42m
            }
        };

        binder.BindParameters(command, parameters, DatabaseProvider.SqlServer);

        Assert.Equal(42m, command.RecordedParameters[0].Value);
    }

    [Fact]
    public void EnumConversion_UsesUnderlyingType()
    {
        var binder = new ParameterBinder(new PassThroughParameterPool(), new ParameterBindingOptions
        {
            ConvertEnumsToUnderlyingType = true
        });
        var command = new RecordingCommand();
        var parameters = new[]
        {
            DalParameter.Input("Status", SampleStatus.Active)
        };

        binder.BindParameters(command, parameters, DatabaseProvider.Oracle);

        Assert.Equal((int)SampleStatus.Active, command.RecordedParameters[0].Value);
    }

    [Fact]
    public void DateTimeOffset_PostgresConvertedToUtcDateTime()
    {
        var binder = new ParameterBinder(new PassThroughParameterPool());
        var command = new RecordingCommand();
        var dto = new DateTimeOffset(2024, 1, 1, 5, 30, 0, TimeSpan.FromHours(-5));
        var parameters = new[]
        {
            DalParameter.Input("OccurredOn", dto)
        };

        binder.BindParameters(command, parameters, DatabaseProvider.PostgreSql);

        var recorded = command.RecordedParameters[0].Value;
        Assert.IsType<DateTime>(recorded);
        Assert.Equal(dto.UtcDateTime, recorded);
    }

    [Fact]
    public void DateTimeOffsetArray_PostgresConvertsEachElement()
    {
        var binder = new ParameterBinder(new PassThroughParameterPool());
        var command = new RecordingCommand();
        var originals = new[]
        {
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.FromHours(2))
        };
        var values = new object?[] { originals[0], originals[1] };
        var parameters = new[]
        {
            new DbParameterDefinition { Name = "Buckets", Value = values }
        };

        binder.BindParameters(command, parameters, DatabaseProvider.PostgreSql);

        var array = Assert.IsType<object?[]>(command.RecordedParameters[0].Value);
        Assert.All(array, item => Assert.IsType<DateTime>(item));
        Assert.Equal(originals[0].UtcDateTime, array[0]);
        Assert.Equal(originals[1].UtcDateTime, array[1]);
    }

    [Fact]
    public void Guid_OracleConvertedToString()
    {
        var binder = new ParameterBinder(new PassThroughParameterPool());
        var command = new RecordingCommand();
        var guid = Guid.Parse("d2719d6f-82bd-4899-a6ac-4d7f87e166ab");
        var parameters = new[]
        {
            DalParameter.Input("Id", guid)
        };

        binder.BindParameters(command, parameters, DatabaseProvider.Oracle);

        Assert.IsType<string>(command.RecordedParameters[0].Value);
        Assert.Equal(guid.ToString("D"), command.RecordedParameters[0].Value);
    }

    [Fact]
    public void ProviderTypeName_WithUnsafeCharacters_Throws()
    {
        var binder = new ParameterBinder(new PassThroughParameterPool());
        var command = new RecordingCommand();
        var parameters = new[]
        {
            new DbParameterDefinition
            {
                Name = "tvp",
                ProviderTypeName = "dbo.Type;DROP TABLE"
            }
        };

        Assert.Throws<ArgumentException>(() => binder.BindParameters(command, parameters, DatabaseProvider.SqlServer));
    }

    [Fact]
    public void ProviderTypeName_UnsafeAllowed_WhenOptionEnabled()
    {
        var binder = new ParameterBinder(new PassThroughParameterPool(), new ParameterBindingOptions
        {
            AllowUnsafeProviderTypeNames = true
        });
        var command = new RecordingCommand();
        var parameters = new[]
        {
            new DbParameterDefinition
            {
                Name = "tvp",
                ProviderTypeName = "dbo.Type;DROP TABLE"
            }
        };

        binder.BindParameters(command, parameters, DatabaseProvider.SqlServer);
        Assert.Equal("dbo.Type;DROP TABLE", ((RecordingParameter)command.RecordedParameters[0]).DataTypeName);
    }

    [Theory]
    [MemberData(nameof(ProviderAndInvalidSizes))]
    public void BindParameters_Throws_WhenSizeIsNotPositive(DatabaseProvider provider, int size)
    {
        var binder = new ParameterBinder(new PassThroughParameterPool());
        var command = new RecordingCommand();
        var parameters = new[]
        {
            new DbParameterDefinition
            {
                Name = "Payload",
                Size = size
            }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => binder.BindParameters(command, parameters, provider));
    }

    [Theory]
    [MemberData(nameof(ProviderAndInvalidPrecisions))]
    public void BindParameters_Throws_WhenPrecisionOutOfRange(DatabaseProvider provider, byte precision)
    {
        var binder = new ParameterBinder(new PassThroughParameterPool());
        var command = new RecordingCommand();
        var parameters = new[]
        {
            new DbParameterDefinition
            {
                Name = "Amount",
                DbType = DbType.Decimal,
                Precision = precision
            }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => binder.BindParameters(command, parameters, provider));
    }

    [Theory]
    [MemberData(nameof(ProviderAndInvalidScales))]
    public void BindParameters_Throws_WhenScaleExceedsSharedLimit(DatabaseProvider provider, byte scale)
    {
        var binder = new ParameterBinder(new PassThroughParameterPool());
        var command = new RecordingCommand();
        var parameters = new[]
        {
            new DbParameterDefinition
            {
                Name = "Amount",
                DbType = DbType.Decimal,
                Scale = scale
            }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => binder.BindParameters(command, parameters, provider));
    }

    [Theory]
    [MemberData(nameof(ProviderPrecisionScalePairs))]
    public void BindParameters_Throws_WhenScaleGreaterThanPrecision(DatabaseProvider provider, byte precision, byte scale)
    {
        var binder = new ParameterBinder(new PassThroughParameterPool());
        var command = new RecordingCommand();
        var parameters = new[]
        {
            new DbParameterDefinition
            {
                Name = "Amount",
                DbType = DbType.Decimal,
                Precision = precision,
                Scale = scale
            }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => binder.BindParameters(command, parameters, provider));
    }

    [Theory]
    [MemberData(nameof(ProviderData))]
    public void BindParameters_Throws_WhenTreatAsListMissingValues(DatabaseProvider provider)
    {
        var binder = new ParameterBinder(new PassThroughParameterPool());
        var command = new RecordingCommand();
        var parameters = new[]
        {
            new DbParameterDefinition
            {
                Name = "Ids",
                DbType = DbType.Int32,
                TreatAsList = true
            }
        };

        Assert.Throws<ArgumentException>(() => binder.BindParameters(command, parameters, provider));
    }

    public static IEnumerable<object[]> ProviderData()
    {
        yield return new object[] { DatabaseProvider.SqlServer };
        yield return new object[] { DatabaseProvider.PostgreSql };
        yield return new object[] { DatabaseProvider.Oracle };
    }

    public static IEnumerable<object[]> ProviderAndInvalidSizes()
    {
        foreach (var provider in new[]
                 {
                     DatabaseProvider.SqlServer,
                     DatabaseProvider.PostgreSql,
                     DatabaseProvider.Oracle
                 })
        {
            yield return new object[] { provider, 0 };
            yield return new object[] { provider, -4 };
        }
    }

    public static IEnumerable<object[]> ProviderAndInvalidPrecisions()
    {
        foreach (var provider in new[]
                 {
                     DatabaseProvider.SqlServer,
                     DatabaseProvider.PostgreSql,
                     DatabaseProvider.Oracle
                 })
        {
            yield return new object[] { provider, (byte)0 };
            yield return new object[] { provider, (byte)40 };
        }
    }

    public static IEnumerable<object[]> ProviderAndInvalidScales()
    {
        foreach (var provider in new[]
                 {
                     DatabaseProvider.SqlServer,
                     DatabaseProvider.PostgreSql,
                     DatabaseProvider.Oracle
                 })
        {
            yield return new object[] { provider, (byte)40 };
        }
    }

    public static IEnumerable<object[]> ProviderPrecisionScalePairs()
    {
        foreach (var provider in new[]
                 {
                     DatabaseProvider.SqlServer,
                     DatabaseProvider.PostgreSql,
                     DatabaseProvider.Oracle
                 })
        {
            yield return new object[] { provider, (byte)10, (byte)12 };
        }
    }

    private enum SampleStatus
    {
        Inactive = 0,
        Active = 1
    }

    private sealed class PassThroughParameterPool : IDbParameterPool
    {
        public bool IsEnabled => false;
        public DbParameter Rent(DbCommand command) => command.CreateParameter();
        public void Return(DbParameter parameter) { }
    }

    private sealed class RecordingCommand : DbCommand
    {
        private readonly RecordingParameterCollection _parameters = new();

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        public IList<DbParameter> RecordedParameters => _parameters.Parameters;

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object ExecuteScalar() => new object();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new RecordingParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new EmptyReader();
    }

    private sealed class RecordingParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ParameterName { get; set; } = string.Empty;
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override object Value { get; set; } = null!;
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public string? DataTypeName { get; set; } = string.Empty;
        public override void ResetDbType() { }
    }

    private sealed class RecordingParameterCollection : DbParameterCollection
    {
        public List<DbParameter> Parameters { get; } = new();
        public override int Count => Parameters.Count;
        public override object SyncRoot => this;
        public override int Add(object value)
        {
            Parameters.Add((DbParameter)value);
            return Parameters.Count - 1;
        }
        public override void AddRange(Array values)
        {
            foreach (DbParameter parameter in values)
            {
                Parameters.Add(parameter);
            }
        }
        public override void Clear() => Parameters.Clear();
        public override bool Contains(object value) => Parameters.Contains((DbParameter)value);
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index) => Parameters.ToArray().CopyTo(array, index);
        public override IEnumerator GetEnumerator() => Parameters.GetEnumerator();
        public override int IndexOf(object value) => Parameters.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, object value) => Parameters.Insert(index, (DbParameter)value);
        public override void Remove(object value) => Parameters.Remove((DbParameter)value);
        public override void RemoveAt(int index) => Parameters.RemoveAt(index);
        public override void RemoveAt(string parameterName) { }
        protected override DbParameter GetParameter(int index) => Parameters[index];
        protected override DbParameter GetParameter(string parameterName) => throw new NotSupportedException();
        protected override void SetParameter(int index, DbParameter value) => Parameters[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => throw new NotSupportedException();
    }

    private sealed class EmptyReader : DbDataReader
    {
        public override bool Read() => false;
        public override int FieldCount => 0;
        public override object this[int ordinal] => null!;
        public override object this[string name] => null!;
        public override int Depth => 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override bool HasRows => false;
        public override bool GetBoolean(int ordinal) => false;
        public override byte GetByte(int ordinal) => 0;
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => '\0';
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override string GetDataTypeName(int ordinal) => string.Empty;
        public override DateTime GetDateTime(int ordinal) => DateTime.MinValue;
        public override decimal GetDecimal(int ordinal) => 0;
        public override double GetDouble(int ordinal) => 0;
        public override Type GetFieldType(int ordinal) => typeof(object);
        public override float GetFloat(int ordinal) => 0;
        public override Guid GetGuid(int ordinal) => Guid.Empty;
        public override short GetInt16(int ordinal) => 0;
        public override int GetInt32(int ordinal) => 0;
        public override long GetInt64(int ordinal) => 0;
        public override string GetName(int ordinal) => string.Empty;
        public override int GetOrdinal(string name) => -1;
        public override string GetString(int ordinal) => string.Empty;
        public override object GetValue(int ordinal) => null!;
        public override int GetValues(object[] values) => 0;
        public override bool IsDBNull(int ordinal) => true;
        public override bool NextResult() => false;
        public override IEnumerator GetEnumerator() => Array.Empty<object?>().GetEnumerator();
    }
}
