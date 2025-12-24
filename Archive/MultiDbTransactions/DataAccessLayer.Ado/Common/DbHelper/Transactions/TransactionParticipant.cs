using System.Data.Common;
using Shared.Configuration;

namespace DataAccessLayer.Transactions;

/// <summary>
/// Represents a single database transaction participant.
/// </summary>
public sealed class TransactionParticipant
{
    public TransactionParticipant(DatabaseOptions options, DbConnection connection, DbTransaction transaction)
    {
        Options = options;
        Connection = connection;
        Transaction = transaction;
    }

    public DatabaseOptions Options { get; }
    public DbConnection Connection { get; }
    public DbTransaction Transaction { get; }
}
