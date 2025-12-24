using System;
using DataAccessLayer.Common.DbHelper.Bulk.Ado;
using Shared.Entities;
using Xunit;

namespace DataAccessLayer.Tests.Bulk;

public sealed class TodoItemBulkMappingTests
{
    [Fact]
    public void MappingProjectsColumnsInOrder()
    {
        var mapping = new TodoItemBulkMapping();
        var sample = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Invoice",
            Notes = "Call customer",
            IsCompleted = false,
            CreatedUtc = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero),
            CompletedUtc = null
        };

        var values = mapping.Project(sample);

        Assert.Equal(mapping.Columns.Count, values.Length);
        Assert.Equal(sample.Id, values[0]);
        Assert.Equal(sample.Title, values[1]);
        Assert.Equal(sample.Notes, values[2]);
        Assert.Equal(sample.IsCompleted, values[3]);
        Assert.Equal(sample.CreatedUtc, values[4]);
        Assert.Equal(sample.CompletedUtc, values[5]);
    }
}

internal sealed class TodoItemBulkMapping : BulkMapping<TodoItem>
{
    public TodoItemBulkMapping()
        : base(
            "TodoItems",
            new[]
            {
                new BulkColumn("Id", System.Data.DbType.Guid, isKey: true),
                new BulkColumn("Title", System.Data.DbType.String),
                new BulkColumn("Notes", System.Data.DbType.String, isNullable: true),
                new BulkColumn("IsCompleted", System.Data.DbType.Boolean),
                new BulkColumn("CreatedUtc", System.Data.DbType.DateTimeOffset),
                new BulkColumn("CompletedUtc", System.Data.DbType.DateTimeOffset, isNullable: true)
            },
            item => new object?[]
            {
                item.Id,
                item.Title,
                item.Notes,
                item.IsCompleted,
                item.CreatedUtc,
                item.CompletedUtc
            })
    {
    }
}
