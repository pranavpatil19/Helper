#nullable enable
#pragma warning disable CS8765

using System;
using System.Data;
using System.Data.Common;
using DataAccessLayer.Providers.Oracle;
using DataAccessLayer.Exceptions;
using Xunit;

namespace DataAccessLayer.Tests.Providers.Oracle;

public static class OracleRefCursorReaderFactoryTests
{
    [Fact]
    public static void Create_ReturnsReader_WhenValueExposesGetDataReader()
    {
        var reader = new FakeReader();
        var parameter = new FakeParameter { Value = new FakeRefCursor(reader) };

        Assert.Same(reader, ((FakeRefCursor)parameter.Value!).GetDataReader());
        var result = OracleRefCursorReaderFactory.Create(parameter);

        Assert.Same(reader, result);
    }

    [Fact]
    public static void Create_Throws_WhenValueIsNull()
    {
        var parameter = new FakeParameter { Value = null };
        Assert.Throws<ProviderFeatureException>(() => OracleRefCursorReaderFactory.Create(parameter));
    }

    [Fact]
    public static void Create_Throws_WhenGetDataReaderMissing()
    {
        var parameter = new FakeParameter { Value = new object() };
        Assert.Throws<ProviderFeatureException>(() => OracleRefCursorReaderFactory.Create(parameter));
    }

    private sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Output;
        public override bool IsNullable { get; set; } = true;
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ParameterName { get; set; } = string.Empty;
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override object Value { get; set; } = null!;
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class FakeRefCursor
    {
        private readonly DbDataReader _reader;

        public FakeRefCursor(DbDataReader reader)
        {
            _reader = reader;
        }

        public DbDataReader GetDataReader() => _reader;
    }

    private sealed class FakeReader : DbDataReader
    {
        public override int Depth => 0;
        public override int FieldCount => 0;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override object this[int ordinal] => throw new NotSupportedException();
        public override object this[string name] => throw new NotSupportedException();
        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override string GetDataTypeName(int ordinal) => string.Empty;
        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
        public override double GetDouble(int ordinal) => throw new NotSupportedException();
        public override System.Collections.IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
        public override Type GetFieldType(int ordinal) => typeof(object);
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override int GetInt32(int ordinal) => throw new NotSupportedException();
        public override long GetInt64(int ordinal) => throw new NotSupportedException();
        public override string GetName(int ordinal) => string.Empty;
        public override int GetOrdinal(string name) => -1;
        public override string GetString(int ordinal) => string.Empty;
        public override object GetValue(int ordinal) => throw new NotSupportedException();
        public override int GetValues(object[] values) => 0;
        public override bool IsDBNull(int ordinal) => true;
        public override bool NextResult() => false;
        public override bool Read() => false;
    }
}
#pragma warning restore CS8765
