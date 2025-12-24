using System;
using System.Data;
using DataAccessLayer.Mapping;
using Xunit;

namespace DataAccessLayer.Tests.Mapping;

public sealed class EnumMappingProfileTests
{
    private readonly EnumMappingProfile _profile = new();

    [Theory]
    [InlineData("Pending", OrderStatus.Pending)]
    [InlineData("completed", OrderStatus.Completed)]
    public void Convert_FromString(string input, OrderStatus expected)
    {
        Assert.True(_profile.TryConvert("status", nameof(Order.Status), typeof(OrderStatus), input, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, OrderStatus.Pending)]
    [InlineData(1, OrderStatus.Completed)]
    public void Convert_FromNumeric(int value, OrderStatus expected)
    {
        Assert.True(_profile.TryConvert("status", nameof(Order.Status), typeof(OrderStatus), value, out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReflectionMapper_UsesProfile_ForEnums()
    {
        var mapper = new ReflectionDataMapper<Order>(profiles: new IMappingProfile[] { _profile });
        var table = new DataTable();
        table.Columns.Add("status", typeof(int));
        table.Rows.Add(1);

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());

        var order = mapper.Map(reader);
        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    private sealed class Order
    {
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
    }

    public enum OrderStatus
    {
        Pending = 0,
        Completed = 1
    }
}
