using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

internal sealed class SqlBulkCopyClient : ISqlBulkCopyClient
{
    private readonly SqlBulkCopy _inner;

    public SqlBulkCopyClient(SqlBulkCopy inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public string DestinationTableName
    {
        get => _inner.DestinationTableName;
        set => _inner.DestinationTableName = value;
    }

    public int BatchSize
    {
        get => _inner.BatchSize;
        set => _inner.BatchSize = value;
    }

    public int BulkCopyTimeout
    {
        get => _inner.BulkCopyTimeout;
        set => _inner.BulkCopyTimeout = value;
    }

    public void AddColumnMapping(string sourceColumn, string destinationColumn) =>
        _inner.ColumnMappings.Add(sourceColumn, destinationColumn);

    public void WriteToServer(IDataReader reader) => _inner.WriteToServer(reader);

    public Task WriteToServerAsync(IDataReader reader, CancellationToken cancellationToken = default) =>
        _inner.WriteToServerAsync(reader, cancellationToken);

    public ValueTask DisposeAsync()
    {
        _inner.Close();
        return ValueTask.CompletedTask;
    }

    public void Dispose() => _inner.Close();
}

internal sealed class SqlBulkCopyClientFactory : ISqlBulkCopyClientFactory
{
    public ISqlBulkCopyClient Create(DbConnection connection, SqlBulkCopyOptions options, DbTransaction? transaction = null)
    {
        if (connection is not SqlConnection sqlConnection)
        {
            throw new BulkOperationException("SqlBulkCopy requires SqlConnection instances.");
        }

        SqlTransaction? sqlTransaction = null;
        if (transaction is not null)
        {
            if (transaction is not SqlTransaction casted)
            {
                throw new BulkOperationException("SqlBulkCopy requires SqlTransaction when a transaction is supplied.");
            }

            sqlTransaction = casted;
        }

        var bulkCopy = new SqlBulkCopy(sqlConnection, options, sqlTransaction);
        return new SqlBulkCopyClient(bulkCopy);
    }
}
