using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace DataAccessLayer.Common.DbHelper.Bulk.Ado;

public interface ISqlBulkCopyClient : IAsyncDisposable, IDisposable
{
    string DestinationTableName { get; set; }
    int BatchSize { get; set; }
    int BulkCopyTimeout { get; set; }
    void AddColumnMapping(string sourceColumn, string destinationColumn);
    void WriteToServer(IDataReader reader);
    Task WriteToServerAsync(IDataReader reader, CancellationToken cancellationToken = default);
}

public interface ISqlBulkCopyClientFactory
{
    ISqlBulkCopyClient Create(DbConnection connection, SqlBulkCopyOptions options, DbTransaction? transaction = null);
}
