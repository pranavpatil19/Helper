using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Provides pooled <see cref="DbCommand"/> instances and binds parameters for DAL executions.
/// </summary>
public sealed class DbCommandFactory : IDbCommandFactory
{
    private readonly IParameterBinder _parameterBinder;
    private readonly IDbParameterPool _parameterPool;
    private readonly ObjectPoolProvider _poolProvider;
    private readonly bool _commandPoolingEnabled;
    private readonly DatabaseOptions _defaultOptions;
    private readonly ILogger<DbCommandFactory> _logger;
    private readonly ConcurrentDictionary<Type, ObjectPool<DbCommand>> _pools = new();
    private readonly ConcurrentDictionary<DbCommand, ObjectPool<DbCommand>> _commandOrigins = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DbCommandFactory"/> class with optional pooling.
    /// </summary>
    /// <param name="parameterBinder">Binder that translates parameter definitions into provider parameters.</param>
    /// <param name="parameterPool">Reusable parameter pool.</param>
    /// <param name="defaultOptions">Default database options (used for timeout inheritance).</param>
    /// <param name="options">Command pooling options.</param>
    /// <param name="logger">Structured logger used for timeout diagnostics.</param>
    public DbCommandFactory(
        IParameterBinder parameterBinder,
        IDbParameterPool parameterPool,
        DatabaseOptions defaultOptions,
        CommandPoolOptions? options = null,
        ILogger<DbCommandFactory>? logger = null)
        : this(parameterBinder, parameterPool, defaultOptions, options, CreateProvider(options), logger ?? NullLogger<DbCommandFactory>.Instance)
    {
    }

    internal DbCommandFactory(
        IParameterBinder parameterBinder,
        IDbParameterPool parameterPool,
        DatabaseOptions defaultOptions,
        CommandPoolOptions? options,
        ObjectPoolProvider poolProvider,
        ILogger<DbCommandFactory> logger)
    {
        _parameterBinder = parameterBinder ?? throw new ArgumentNullException(nameof(parameterBinder));
        _parameterPool = parameterPool ?? throw new ArgumentNullException(nameof(parameterPool));
        _defaultOptions = defaultOptions ?? throw new ArgumentNullException(nameof(defaultOptions));
        var poolOptions = options ?? new CommandPoolOptions();
        _commandPoolingEnabled = poolOptions.EnableCommandPooling;
        _poolProvider = poolProvider ?? throw new ArgumentNullException(nameof(poolProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Rents a command for the supplied connection/request pair and prepares it when requested.
    /// </summary>
    /// <param name="connection">Open database connection.</param>
    /// <param name="request">Command request describing text, parameters, and timeouts.</param>
    /// <returns>A provider command ready for execution.</returns>
    public DbCommand Rent(DbConnection connection, DbCommandRequest request)
    {
        var command = RentCore(connection, request);
        if (request.PrepareCommand)
        {
            command.PrepareAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        return command;
    }

    /// <summary>
    /// Rents a command asynchronously for the supplied connection/request pair.
    /// </summary>
    /// <param name="connection">Open database connection.</param>
    /// <param name="request">Command request describing text, parameters, and timeouts.</param>
    /// <param name="cancellationToken">Propagation token for asynchronous preparation.</param>
    /// <returns>A provider command ready for execution.</returns>
    public async Task<DbCommand> RentAsync(DbConnection connection, DbCommandRequest request, CancellationToken cancellationToken = default)
    {
        var command = RentCore(connection, request);
        if (request.PrepareCommand)
        {
            await command.PrepareAsync(cancellationToken).ConfigureAwait(false);
        }

        return command;
    }

    /// <summary>
    /// Returns a command to the pool (or disposes it when pooling is disabled).
    /// </summary>
    /// <param name="command">Command to recycle.</param>
    public void Return(DbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_commandPoolingEnabled && _commandOrigins.TryRemove(command, out var pool))
        {
            pool.Return(command);
            return;
        }

        ReturnParameters(command);
        command.Dispose();
    }

    private DbCommand RentCore(DbConnection connection, DbCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);

        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Connection must be open before renting commands.");
        }

        var provider = (request.OverrideOptions ?? _defaultOptions).Provider;
        var effectiveRequest = NormalizeListParameters(provider, request);

        DbCommand command;
        ObjectPool<DbCommand>? pool = null;
        if (_commandPoolingEnabled)
        {
            pool = _pools.GetOrAdd(connection.GetType(), _ => CreatePool(connection));
            command = pool.Get();
            _commandOrigins[command] = pool;
        }
        else
        {
            command = connection.CreateCommand();
        }

        command.Connection = connection;
        command.Transaction = effectiveRequest.Transaction;
        command.CommandText = effectiveRequest.CommandText;
        command.CommandType = effectiveRequest.CommandType;
        var diagnostics = effectiveRequest.OverrideOptions?.Diagnostics ?? _defaultOptions.Diagnostics ?? new DiagnosticsOptions();
        var effectiveTimeout = ResolveCommandTimeout(effectiveRequest);
        if (effectiveTimeout is { } timeout)
        {
            command.CommandTimeout = timeout;
            LogCommandTimeout(effectiveRequest, diagnostics, provider, timeout);
        }

        command.Parameters.Clear();
        if (effectiveRequest.Parameters.Count > 0)
        {
            _parameterBinder.BindParameters(command, effectiveRequest.Parameters, provider);
        }

        return command;
    }

    private ObjectPool<DbCommand> CreatePool(DbConnection connection)
    {
        var prototype = connection.CreateCommand();
        var commandType = prototype.GetType();
        var policy = new DbCommandPooledObjectPolicy(commandType, _parameterPool);
        var pool = _poolProvider.Create(policy);
        pool.Return(prototype);
        return pool;
    }

    private void ReturnParameters(DbCommand command)
    {
        if (!_parameterPool.IsEnabled)
        {
            return;
        }

        foreach (DbParameter parameter in command.Parameters)
        {
            _parameterPool.Return(parameter);
        }
    }

    private static ObjectPoolProvider CreateProvider(CommandPoolOptions? options)
    {
        var provider = new DefaultObjectPoolProvider();
        if (options is not null)
        {
            provider.MaximumRetained = options.MaximumRetainedCommands;
        }

        return provider;
    }

    private int? ResolveCommandTimeout(DbCommandRequest request)
    {
        if (request.CommandTimeoutSeconds is { } explicitTimeout)
        {
            return explicitTimeout;
        }

        if (request.OverrideOptions?.CommandTimeoutSeconds is { } overrideTimeout)
        {
            return overrideTimeout;
        }

        return _defaultOptions.CommandTimeoutSeconds;
    }

    private void LogCommandTimeout(DbCommandRequest request, DiagnosticsOptions diagnostics, DatabaseProvider provider, int timeout)
    {
        if (!diagnostics.LogEffectiveTimeouts)
        {
            return;
        }

        var trace = request.TraceName ?? request.CommandText;

        _logger.LogInformation(
            "Applied command timeout {TimeoutSeconds}s for {TraceName} (Provider: {Provider}).",
            timeout,
            trace,
            provider);
    }

    private DbCommandRequest NormalizeListParameters(DatabaseProvider provider, DbCommandRequest request)
    {
        return provider switch
        {
            DatabaseProvider.SqlServer => ExpandSqlServerLists(request),
            DatabaseProvider.PostgreSql => ConvertListsToArrays(request),
            DatabaseProvider.Oracle => ConvertListsToArrays(request),
            _ => request
        };
    }

    private static DbCommandRequest ExpandSqlServerLists(DbCommandRequest request)
    {
        if (request.Parameters.Count == 0 || request.CommandType != CommandType.Text)
        {
            return request;
        }

        var updatedParameters = new List<DbParameterDefinition>(request.Parameters.Count);
        var commandText = request.CommandText;
        var changed = false;

        foreach (var parameter in request.Parameters)
        {
            if (!parameter.TreatAsList || parameter.Values is null)
            {
                updatedParameters.Add(parameter);
                continue;
            }

            if (parameter.Values.Count == 0)
            {
                throw new InvalidOperationException($"List parameter '{parameter.Name}' must contain at least one value when targeting SQL Server text commands.");
            }

            var replacementTokens = new List<string>(parameter.Values.Count);
            for (var i = 0; i < parameter.Values.Count; i++)
            {
                var newName = $"{parameter.Name}_{i}";
                replacementTokens.Add("@" + newName);
                updatedParameters.Add(CloneWithValue(parameter, newName, parameter.Values[i]));
            }

            var originalToken = "@" + parameter.Name;
            var replacement = string.Join(",", replacementTokens);
            var newCommandText = ReplaceParameterToken(commandText, originalToken, replacement);
            if (newCommandText == commandText)
            {
                throw new InvalidOperationException($"Command text does not reference list parameter '{originalToken}'.");
            }

            commandText = newCommandText;
            changed = true;
        }

        return changed ? CloneRequest(request, commandText, updatedParameters) : request;
    }

    private static DbCommandRequest ConvertListsToArrays(DbCommandRequest request)
    {
        if (request.Parameters.Count == 0)
        {
            return request;
        }

        var changed = false;
        var updatedParameters = new List<DbParameterDefinition>(request.Parameters.Count);
        foreach (var parameter in request.Parameters)
        {
            if (parameter.TreatAsList && parameter.Values is not null)
            {
                changed = true;
                updatedParameters.Add(CloneWithArray(parameter, ToObjectArray(parameter.Values)));
            }
            else
            {
                updatedParameters.Add(parameter);
            }
        }

        return changed ? CloneRequest(request, request.CommandText, updatedParameters) : request;
    }

    private static object?[] ToObjectArray(IReadOnlyList<object?> values)
    {
        var result = new object?[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            result[i] = values[i];
        }

        return result;
    }

    private static DbParameterDefinition CloneWithValue(DbParameterDefinition template, string name, object? value)
    {
        return new DbParameterDefinition
        {
            Name = name,
            Value = value,
            DbType = template.DbType,
            Direction = ParameterDirection.Input,
            Size = template.Size,
            Precision = template.Precision,
            Scale = template.Scale,
            IsNullable = template.IsNullable || value is null,
            DefaultValue = template.DefaultValue,
            ProviderTypeName = template.ProviderTypeName,
            TreatAsList = false,
            Values = null,
            ValueConverter = template.ValueConverter
        };
    }

    private static DbParameterDefinition CloneWithArray(DbParameterDefinition template, object? arrayValue)
    {
        return new DbParameterDefinition
        {
            Name = template.Name,
            Value = arrayValue,
            DbType = template.DbType,
            Direction = template.Direction,
            Size = template.Size,
            Precision = template.Precision,
            Scale = template.Scale,
            IsNullable = template.IsNullable,
            DefaultValue = template.DefaultValue,
            ProviderTypeName = template.ProviderTypeName,
            TreatAsList = false,
            Values = null,
            ValueConverter = template.ValueConverter
        };
    }

    private static DbCommandRequest CloneRequest(
        DbCommandRequest source,
        string commandText,
        IReadOnlyList<DbParameterDefinition> parameters)
    {
        return new DbCommandRequest
        {
            CommandText = commandText,
            CommandType = source.CommandType,
            Parameters = parameters,
            CommandTimeoutSeconds = source.CommandTimeoutSeconds,
            PrepareCommand = source.PrepareCommand,
            Connection = source.Connection,
            CloseConnection = source.CloseConnection,
            Transaction = source.Transaction,
            OverrideOptions = source.OverrideOptions,
            CommandBehavior = source.CommandBehavior,
            TraceName = source.TraceName
        };
    }

    private static string ReplaceParameterToken(string text, string token, string replacement)
    {
        var stringBuilder = new StringBuilder(text.Length);
        var index = 0;

        while (index < text.Length)
        {
            var matchIndex = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0)
            {
                stringBuilder.Append(text.AsSpan(index));
                break;
            }

            var afterIndex = matchIndex + token.Length;
            if (afterIndex < text.Length && IsIdentifierChar(text[afterIndex]))
            {
                stringBuilder.Append(text.AsSpan(index, afterIndex - index));
                index = afterIndex;
                continue;
            }

            stringBuilder.Append(text.AsSpan(index, matchIndex - index));
            stringBuilder.Append(replacement);
            index = afterIndex;
        }

        return stringBuilder.ToString();
    }

    private static bool IsIdentifierChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_';

    private sealed class DbCommandPooledObjectPolicy : PooledObjectPolicy<DbCommand>
    {
        private readonly Type _commandType;
        private readonly IDbParameterPool _parameterPool;

        public DbCommandPooledObjectPolicy(Type commandType, IDbParameterPool parameterPool)
        {
            _commandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
            _parameterPool = parameterPool ?? throw new ArgumentNullException(nameof(parameterPool));
        }

        public override DbCommand Create() => (DbCommand)Activator.CreateInstance(_commandType)!;

        public override bool Return(DbCommand obj)
        {
            if (_parameterPool.IsEnabled)
            {
                foreach (DbParameter parameter in obj.Parameters)
                {
                    _parameterPool.Return(parameter);
                }
            }

            obj.Parameters.Clear();
            obj.Transaction = null;
            obj.Connection = null;
            obj.CommandText = string.Empty;
            obj.CommandType = CommandType.Text;
            obj.CommandTimeout = 30;
            return true;
        }
    }
}
