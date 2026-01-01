using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;
using DataAccessLayer.Mapping;

namespace DataAccessLayer.Common.DbHelper;

public sealed partial class DatabaseHelper
{
    /// <summary>
    /// Executes a stored procedure asynchronously and returns rows affected plus output parameters.
    /// </summary>
    public Task<DbExecutionResult> ExecuteStoredProcedureAsync(
        string procedureName,
        IReadOnlyList<DbParameterDefinition>? parameters = null,
        CancellationToken cancellationToken = default) =>
        ExecuteStoredProcedureRequestAsync(
            procedureName,
            parameters,
            (request, token) => ExecuteAsync(request, token),
            cancellationToken);

    /// <summary>
    /// Executes a stored procedure synchronously.
    /// </summary>
    public DbExecutionResult ExecuteStoredProcedure(
        string procedureName,
        IReadOnlyList<DbParameterDefinition>? parameters = null) =>
        ExecuteStoredProcedureRequest(
            procedureName,
            parameters,
            Execute);

    /// <summary>
    /// Executes a stored procedure that returns result sets asynchronously.
    /// </summary>
    public Task<DbQueryResult<IReadOnlyList<T>>> QueryStoredProcedureAsync<T>(
        string procedureName,
        Func<DbDataReader, T> mapper,
        IReadOnlyList<DbParameterDefinition>? parameters = null,
        CancellationToken cancellationToken = default) =>
        ExecuteStoredProcedureRequestAsync(
            procedureName,
            parameters,
            (request, token) => QueryAsync(request, mapper, token),
            cancellationToken);

    /// <summary>
    /// Executes a stored procedure that returns result sets asynchronously and relies on the built-in row mapper.
    /// </summary>
    public Task<DbQueryResult<IReadOnlyList<T>>> QueryStoredProcedureAsync<T>(
        string procedureName,
        RowMapperRequest? mapperRequest = null,
        IReadOnlyList<DbParameterDefinition>? parameters = null,
        CancellationToken cancellationToken = default)
        where T : class =>
        ExecuteStoredProcedureRequestAsync(
            procedureName,
            parameters,
            (request, token) => QueryAsync<T>(request, mapperRequest, token),
            cancellationToken);

    /// <summary>
    /// Executes a stored procedure that returns result sets synchronously.
    /// </summary>
    public DbQueryResult<IReadOnlyList<T>> QueryStoredProcedure<T>(
        string procedureName,
        Func<DbDataReader, T> mapper,
        IReadOnlyList<DbParameterDefinition>? parameters = null) =>
        ExecuteStoredProcedureRequest(
            procedureName,
            parameters,
            request => Query(request, mapper));

    /// <summary>
    /// Executes a stored procedure synchronously using the built-in row mapper.
    /// </summary>
    public DbQueryResult<IReadOnlyList<T>> QueryStoredProcedure<T>(
        string procedureName,
        RowMapperRequest? mapperRequest = null,
        IReadOnlyList<DbParameterDefinition>? parameters = null)
        where T : class =>
        ExecuteStoredProcedureRequest(
            procedureName,
            parameters,
            request => Query<T>(request, mapperRequest));

    /// <summary>
    /// Executes a stored procedure and returns a reader lease asynchronously.
    /// </summary>
    public Task<DbReaderScope> ExecuteStoredProcedureReaderAsync(
        string procedureName,
        IReadOnlyList<DbParameterDefinition>? parameters = null,
        CancellationToken cancellationToken = default) =>
        ExecuteStoredProcedureRequestAsync(
            procedureName,
            parameters,
            (request, token) => ExecuteReaderAsync(request, token),
            cancellationToken);

    /// <summary>
    /// Executes a stored procedure and returns a reader lease synchronously.
    /// </summary>
    public DbReaderScope ExecuteStoredProcedureReader(
        string procedureName,
        IReadOnlyList<DbParameterDefinition>? parameters = null) =>
        ExecuteStoredProcedureRequest(
            procedureName,
            parameters,
            ExecuteReader);
}
