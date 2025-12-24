using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using DataAccessLayer.Mapping;

namespace Data.Benchmarks;

[MemoryDiagnoser]
public class ReaderMappingBenchmarks
{
    private readonly ReusableReader _reader;
    private readonly ReflectionDataMapper<SampleEntity> _reflectionMapper = new();
    private readonly IlEmitDataMapper<SampleEntity> _ilMapper = new();
    private readonly IDataMapper<SampleGeneratedEntity> _generatedMapper =
        DataMapperFactory.CreateMapper<SampleGeneratedEntity>(MapperStrategy.SourceGenerator);

    public ReaderMappingBenchmarks()
    {
        var rows = new List<object?[]>();
        for (var i = 0; i < 500; i++)
        {
            rows.Add(new object?[] { i, $"Name {i}" });
        }

        _reader = new ReusableReader(new[] { "Id", "Name" }, rows);
    }

    [IterationSetup]
    public void Reset() => _reader.Reset();

    [Benchmark]
    public int ReflectionMapper()
    {
        var count = 0;
        while (_reader.Read())
        {
            var entity = _reflectionMapper.Map(_reader);
            count += entity.Id;
        }

        return count;
    }

    [Benchmark]
    public int IlEmitMapper()
    {
        var count = 0;
        _reader.Reset();
        while (_reader.Read())
        {
            var entity = _ilMapper.Map(_reader);
            count += entity.Id;
        }

        return count;
    }

    [Benchmark]
    public int GeneratedMapper()
    {
        var count = 0;
        _reader.Reset();
        while (_reader.Read())
        {
            var entity = _generatedMapper.Map(_reader);
            count += entity.Id;
        }

        return count;
    }

    private sealed class SampleEntity
    {
        [SuppressMessage("SonarAnalyzer.CSharp", "S1144", Justification = "Properties are populated via reflection during benchmarks.")]
        [SuppressMessage("SonarAnalyzer.CSharp", "S3459", Justification = "Values are set by the mapper at runtime.")]
        public int Id { get; set; }

        [SuppressMessage("SonarAnalyzer.CSharp", "S1144", Justification = "Properties are populated via reflection during benchmarks.")]
        [SuppressMessage("SonarAnalyzer.CSharp", "S3459", Justification = "Values are set by the mapper at runtime.")]
        public string? Name { get; set; }
    }

    [GeneratedMapper("BenchmarkSampleEntityMapper")]
    private sealed class SampleGeneratedEntity
    {
        [SuppressMessage("SonarAnalyzer.CSharp", "S1144", Justification = "Properties are populated via reflection during benchmarks.")]
        [SuppressMessage("SonarAnalyzer.CSharp", "S3459", Justification = "Values are set by the mapper at runtime.")]
        public int Id { get; set; }

        [SuppressMessage("SonarAnalyzer.CSharp", "S1144", Justification = "Properties are populated via reflection during benchmarks.")]
        [SuppressMessage("SonarAnalyzer.CSharp", "S3459", Justification = "Values are set by the mapper at runtime.")]
        public string? Name { get; set; }
    }

    private sealed class ReusableReader : DbDataReader
    {
        private readonly string[] _columns;
        private readonly List<object?[]> _rows;
        private int _index = -1;

        public ReusableReader(string[] columns, List<object?[]> rows)
        {
            _columns = columns;
            _rows = rows;
        }

        public void Reset() => _index = -1;

        public override bool Read()
        {
            _index++;
            return _index < _rows.Count;
        }

        public override int FieldCount => _columns.Length;
        public override string GetName(int ordinal) => _columns[ordinal];
        public override int GetOrdinal(string name)
        {
            for (var i = 0; i < _columns.Length; i++)
            {
                if (string.Equals(_columns[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
        public override Type GetFieldType(int ordinal) => _rows[_index][ordinal]?.GetType() ?? typeof(object);
        public override object GetValue(int ordinal) => _rows[_index][ordinal]!;
        public override int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, FieldCount);
            Array.Copy(_rows[_index], values, count);
            return count;
        }
        public override bool IsDBNull(int ordinal) => _rows[_index][ordinal] is null;
        public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
        public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
        public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
        public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
        public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
        public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
        public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
        public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal)) ?? string.Empty;
        public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
        public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
        public override IEnumerator GetEnumerator() => ((IEnumerable)_rows[_index]).GetEnumerator();
        public override bool NextResult() => false;
        public override bool HasRows => true;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));
    }
}
