using System;
using System.Collections.Generic;
using System.Data;
using DataAccessLayer.Configuration;
using DataAccessLayer.Mapping;
using Xunit;

namespace DataAccessLayer.Tests;

public sealed class DataTableMappingExtensionsTests
{
    private readonly RowMapperFactory _factory = new(new DbHelperOptions());

    [Fact]
    public void MapRows_FromDataTable_ProjectsAllRows()
    {
        var table = CreateTable();
        table.Rows.Add(1, "alpha");
        table.Rows.Add(2, "beta");

        var result = table.MapRows<TestRecord>(_factory);

        var materialized = Assert.IsType<TestRecord[]>(result);
        Assert.Collection(
            materialized,
            first =>
            {
                Assert.Equal(1, first.Id);
                Assert.Equal("alpha", first.Name);
            },
            second =>
            {
                Assert.Equal(2, second.Id);
                Assert.Equal("beta", second.Name);
            });
    }

    [Fact]
    public void MapRows_FromDataSet_ByName()
    {
        var dataSet = new DataSet();
        var table = dataSet.Tables.Add("payload");
        InitializeTableSchema(table);
        table.Rows.Add(42, "answer");

        var result = dataSet.MapRows<TestRecord>("payload", _factory);

        var materialized = Assert.IsType<TestRecord[]>(result);
        Assert.Single(materialized);
        Assert.Equal(42, materialized[0].Id);
    }

    [Fact]
    public void MapRows_FromDataSet_ByIndex_WithColumnMap()
    {
        var dataSet = new DataSet();
        dataSet.Tables.Add("ignored");
        var targetTable = dataSet.Tables.Add();
        targetTable.Columns.Add("Identifier", typeof(int));
        targetTable.Columns.Add("DisplayName", typeof(string));
        targetTable.Rows.Add(7, "gamma");

        var request = new RowMapperRequest
        {
            PropertyToColumnMap = new System.Collections.Generic.Dictionary<string, string>
            {
                [nameof(TestRecord.Id)] = "Identifier",
                [nameof(TestRecord.Name)] = "DisplayName"
            }
        };

        var result = dataSet.MapRows<TestRecord>(1, _factory, request);

        var materialized = Assert.IsType<TestRecord[]>(result);
        Assert.Single(materialized);
        Assert.Equal("gamma", materialized[0].Name);
    }

    [Fact]
    public void MapRows_WithOracleStyleColumns_CoercesTypes()
    {
        var table = new DataTable();
        table.Columns.Add("CUSTOMER_ID", typeof(decimal));
        table.Columns.Add("IS_PREFERRED", typeof(decimal));
        table.Rows.Add(1m, 1m);
        table.Rows.Add(2m, 0m);

        var mapperRequest = new RowMapperRequest
        {
            PropertyToColumnMap = new Dictionary<string, string>
            {
                [nameof(OracleCustomerDto.Id)] = "CUSTOMER_ID",
                [nameof(OracleCustomerDto.IsPreferred)] = "IS_PREFERRED"
            }
        };

        var result = table.MapRows<OracleCustomerDto>(_factory, mapperRequest);

        var materialized = Assert.IsType<OracleCustomerDto[]>(result);
        Assert.True(materialized[0].IsPreferred);
        Assert.False(materialized[1].IsPreferred);
    }

    [Fact]
    public void MapRows_WithProviderProfiles_ConvertsDatesEnumsAndBooleans()
    {
        var profiles = new IMappingProfile[]
        {
            new OracleBooleanMappingProfile(),
            new OracleDateTimeMappingProfile(),
            new EnumMappingProfile()
        };
        var factory = new RowMapperFactory(new DbHelperOptions(), profiles);
        var table = new DataTable();
        table.Columns.Add("CUSTOMER_ID", typeof(decimal));
        table.Columns.Add("IS_PREFERRED", typeof(string));
        table.Columns.Add("CREATED_ON", typeof(DateTime));
        table.Columns.Add("STATUS_CODE", typeof(string));
        table.Rows.Add(10m, "Y", new DateTime(2024, 1, 1, 8, 30, 0, DateTimeKind.Utc), "Active");
        table.Rows.Add(11m, "N", new DateTime(2024, 2, 1, 9, 0, 0, DateTimeKind.Utc), "Disabled");

        var request = new RowMapperRequest
        {
            PropertyToColumnMap = new Dictionary<string, string>
            {
                [nameof(CustomerSnapshot.Id)] = "CUSTOMER_ID",
                [nameof(CustomerSnapshot.IsPreferred)] = "IS_PREFERRED",
                [nameof(CustomerSnapshot.CreatedUtc)] = "CREATED_ON",
                [nameof(CustomerSnapshot.Status)] = "STATUS_CODE"
            }
        };

        var result = table.MapRows<CustomerSnapshot>(factory, request);

        var customers = Assert.IsType<CustomerSnapshot[]>(result);
        Assert.Equal(2, customers.Length);
        Assert.Equal(10, customers[0].Id);
        Assert.True(customers[0].IsPreferred);
        Assert.Equal(OrderStatus.Active, customers[0].Status);
        Assert.Equal(DateTimeKind.Utc, customers[0].CreatedUtc.UtcDateTime.Kind);
        Assert.Equal(11, customers[1].Id);
        Assert.False(customers[1].IsPreferred);
        Assert.Equal(OrderStatus.Disabled, customers[1].Status);
    }

    [Fact]
    public void MapRows_EmptyTable_ReturnsSharedArray()
    {
        var table = CreateTable();

        var result = table.MapRows<TestRecord>(_factory);

        Assert.Same(Array.Empty<TestRecord>(), result);
    }

    private static DataTable CreateTable()
    {
        var table = new DataTable();
        InitializeTableSchema(table);
        return table;
    }

    private static void InitializeTableSchema(DataTable table)
    {
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
    }

    private sealed class TestRecord
    {
        public int Id { get; set; } = 0;
        public string? Name { get; set; } = string.Empty;
    }

    private sealed class OracleCustomerDto
    {
        public int Id { get; set; } = 0;
        public bool IsPreferred { get; set; } = false;
    }

    private sealed class CustomerSnapshot
    {
        public int Id { get; set; } = 0;
        public bool IsPreferred { get; set; } = false;
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.MinValue;
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
    }

    private enum OrderStatus
    {
        Pending = 0,
        Active = 1,
        Disabled = 2
    }
}
