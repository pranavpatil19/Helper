using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;
using DataAccessLayer.Mapping;

namespace DataAccessLayer.Common.DbHelper;

public sealed partial class DatabaseHelper
{
    /// <summary>
    /// Executes a data reader asynchronously and materializes each row with the supplied mapper.
    /// </summary>
    /// <typeparam name="T">Type produced by the mapper.</typeparam>
    /// <param name="request">Command request describing text, parameters, and transaction context.</param>
    /// <param name="mapper">Projection delegate invoked per row.</param>
    /// <param name="cancellationToken">Propagation token for the async operation.</param>
    /// <returns>A <see cref="DbQueryResult{T}"/> containing materialized rows and execution metadata.</returns>
    public Task<DbQueryResult<IReadOnlyList<T>>> QueryAsync<T>(DbCommandRequest request, Func<DbDataReader, T> mapper, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return ExecuteBufferedQueryAsync(
            nameof(QueryAsync),
            request,
            async (command, innerToken) =>
            {
                var behavior = request.CommandBehavior == CommandBehavior.Default
                    ? CommandBehavior.Default
                    : request.CommandBehavior;

                await using var reader = await ExecuteReaderWithFallbackAsync(request, command, behavior, innerToken).ConfigureAwait(false);
                var buffer = new List<T>();
                while (await reader.ReadAsync(innerToken).ConfigureAwait(false))
                {
                    buffer.Add(mapper(reader));
                }

                var rows = reader.RecordsAffected;
                return new CommandResult<IReadOnlyList<T>>(buffer, rows);
            },
            static data => data.Count,
            cancellationToken);
    }

    /// <summary>
    /// Executes a data reader asynchronously and materializes each row using the configured row-mapper pipeline.
    /// </summary>
    /// <typeparam name="T">Destination type.</typeparam>
    /// <param name="request">Command request describing text, parameters, and transaction context.</param>
    /// <param name="mapperRequest">Optional overrides (strategy, casing, column map).</param>
    /// <param name="cancellationToken">Propagation token for the async operation.</param>
    /// <returns>A <see cref="DbQueryResult{T}"/> containing materialized rows and execution metadata.</returns>
    public Task<DbQueryResult<IReadOnlyList<T>>> QueryAsync<T>(
        DbCommandRequest request,
        RowMapperRequest? mapperRequest = null,
        CancellationToken cancellationToken = default)
        where T : class =>
        QueryAsync(request, ResolveRowMapper<T>(mapperRequest), cancellationToken);

    /// <summary>
    /// Executes a data reader synchronously and materializes each row with the supplied mapper.
    /// </summary>
    /// <typeparam name="T">Type produced by the mapper.</typeparam>
    /// <param name="request">Command request describing text, parameters, and transaction context.</param>
    /// <param name="mapper">Projection delegate invoked per row.</param>
    /// <returns>A <see cref="DbQueryResult{T}"/> containing materialized rows and execution metadata.</returns>
    public DbQueryResult<IReadOnlyList<T>> Query<T>(DbCommandRequest request, Func<DbDataReader, T> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return ExecuteBufferedQuery(
            nameof(Query),
            request,
            command =>
            {
                var behavior = request.CommandBehavior == CommandBehavior.Default
                    ? CommandBehavior.Default
                    : request.CommandBehavior;

                using var reader = command.ExecuteReader(behavior);
                var buffer = new List<T>();
                while (reader.Read())
                {
                    buffer.Add(mapper(reader));
                }

                var rows = reader.RecordsAffected;
                return new CommandResult<IReadOnlyList<T>>(buffer, rows);
            },
            static data => data.Count);
    }

    /// <summary>
    /// Executes a data reader synchronously using the configured row-mapper pipeline.
    /// </summary>
    public DbQueryResult<IReadOnlyList<T>> Query<T>(DbCommandRequest request, RowMapperRequest? mapperRequest = null)
        where T : class =>
        Query(request, ResolveRowMapper<T>(mapperRequest));

    /// <summary>
    /// Loads the results into a <see cref="DataTable"/> asynchronously.
    /// </summary>
    /// <param name="request">Command request describing text, parameters, and transaction context.</param>
    /// <param name="cancellationToken">Propagation token for the async operation.</param>
    /// <returns>A <see cref="DbQueryResult{DataTable}"/> containing the populated table.</returns>
    public Task<DbQueryResult<DataTable>> LoadDataTableAsync(DbCommandRequest request, CancellationToken cancellationToken = default) =>
        ExecuteBufferedQueryAsync(
            nameof(LoadDataTableAsync),
            request,
            async (command, innerToken) =>
            {
                await using var reader = await ExecuteReaderWithFallbackAsync(request, command, request.CommandBehavior, innerToken).ConfigureAwait(false);
                var table = new DataTable();
                table.Load(reader);
                var rows = reader.RecordsAffected;
                return new CommandResult<DataTable>(table, rows);
            },
            static table => table.Rows.Count,
            cancellationToken);

    /// <summary>
    /// Loads the results into a <see cref="DataTable"/> synchronously.
    /// </summary>
    /// <param name="request">Command request describing text, parameters, and transaction context.</param>
    /// <returns>A <see cref="DbQueryResult{DataTable}"/> containing the populated table.</returns>
    public DbQueryResult<DataTable> LoadDataTable(DbCommandRequest request) =>
        ExecuteBufferedQuery(
            nameof(LoadDataTable),
            request,
            command =>
            {
                using var reader = command.ExecuteReader(request.CommandBehavior);
                var table = new DataTable();
                table.Load(reader);
                var rows = reader.RecordsAffected;
                return new CommandResult<DataTable>(table, rows);
            },
            static table => table.Rows.Count);

    /// <summary>
    /// Loads all result sets into a <see cref="DataSet"/> asynchronously.
    /// </summary>
    /// <param name="request">Command request describing text, parameters, and transaction context.</param>
    /// <param name="cancellationToken">Propagation token for the async operation.</param>
    /// <returns>A <see cref="DbQueryResult{DataSet}"/> containing tables and metadata.</returns>
    public Task<DbQueryResult<DataSet>> LoadDataSetAsync(DbCommandRequest request, CancellationToken cancellationToken = default) =>
        ExecuteBufferedQueryAsync(
            nameof(LoadDataSetAsync),
            request,
            async (command, innerToken) =>
            {
                await using var reader = await ExecuteReaderWithFallbackAsync(request, command, request.CommandBehavior, innerToken).ConfigureAwait(false);
                var dataSet = await ReadDataSetAsync(reader, innerToken).ConfigureAwait(false);
                var rows = reader.RecordsAffected;
                return new CommandResult<DataSet>(dataSet, rows);
            },
            static dataSet => dataSet.Tables.Count,
            cancellationToken);

    /// <summary>
    /// Loads all result sets into a <see cref="DataSet"/> synchronously.
    /// </summary>
    /// <param name="request">Command request describing text, parameters, and transaction context.</param>
    /// <returns>A <see cref="DbQueryResult{DataSet}"/> containing tables and metadata.</returns>
    public DbQueryResult<DataSet> LoadDataSet(DbCommandRequest request) =>
        ExecuteBufferedQuery(
            nameof(LoadDataSet),
            request,
            command =>
            {
                using var reader = command.ExecuteReader(request.CommandBehavior);
                var dataSet = ReadDataSet(reader);
                var rows = reader.RecordsAffected;
                return new CommandResult<DataSet>(dataSet, rows);
            },
            static dataSet => dataSet.Tables.Count);
}
