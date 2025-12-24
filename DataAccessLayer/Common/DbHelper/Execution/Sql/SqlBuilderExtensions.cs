using System.Data;
using Shared.Configuration;

namespace DataAccessLayer.Execution;

/// <summary>
/// Convenience helpers for converting <see cref="SqlBuilder"/> output into DAL requests.
/// </summary>
public static class SqlBuilderExtensions
{
    public static SqlBuilderResult Build(this SqlBuilder builder, DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return builder.Build(options.Provider);
    }

    public static DbCommandRequest ToCommandRequest(
        this SqlBuilder builder,
        DatabaseOptions options,
        CommandType commandType = CommandType.Text)
    {
        var result = builder.Build(options);
        return result.ToRequest(commandType);
    }
}
