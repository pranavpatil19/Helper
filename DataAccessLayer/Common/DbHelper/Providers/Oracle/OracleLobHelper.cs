using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace DataAccessLayer.Providers.Oracle;

/// <summary>
/// Streaming helpers for Oracle LOBs (BLOB/CLOB).
/// </summary>
public static class OracleLobHelper
{
    public static async Task StreamBlobAsync(
        OracleBlob blob,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blob);
        ArgumentNullException.ThrowIfNull(destination);

        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long total = 0;
            while (total < blob.Length)
            {
                var read = await blob.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                total += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task StreamClobAsync(
        OracleClob clob,
        TextWriter writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clob);
        ArgumentNullException.ThrowIfNull(writer);

        var buffer = ArrayPool<char>.Shared.Rent(4096);
        try
        {
            long total = 0;
            while (total < clob.Length)
            {
                var read = await clob.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await writer.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                total += read;
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}
