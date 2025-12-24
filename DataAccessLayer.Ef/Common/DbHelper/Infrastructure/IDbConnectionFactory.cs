using System.Data.Common;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection(DatabaseOptions options);
}
