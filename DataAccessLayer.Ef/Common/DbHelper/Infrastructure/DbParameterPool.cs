using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Microsoft.Extensions.ObjectPool;
using Shared.Configuration;

namespace DataAccessLayer.Common.DbHelper;

/// <summary>
/// Default implementation that reuses provider-specific DbParameter instances.
/// </summary>
public sealed class DbParameterPool : IDbParameterPool
{
    private readonly CommandPoolOptions _options;
    private readonly ObjectPoolProvider _poolProvider;
    private readonly ConcurrentDictionary<Type, ObjectPool<DbParameter>> _pools = new();
    private readonly ConcurrentDictionary<DbParameter, ObjectPool<DbParameter>> _parameterOrigins = new();

    public DbParameterPool(CommandPoolOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _poolProvider = new DefaultObjectPoolProvider
        {
            MaximumRetained = options.MaximumRetainedParameters
        };
    }

    public bool IsEnabled => _options.EnableParameterPooling;

    public DbParameter Rent(DbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!IsEnabled)
        {
            return command.CreateParameter();
        }

        var pool = _pools.GetOrAdd(command.GetType(), _ => CreatePool(command));
        var parameter = pool.Get();
        _parameterOrigins[parameter] = pool;
        return parameter;
    }

    public void Return(DbParameter parameter)
    {
        if (!IsEnabled || parameter is null)
        {
            return;
        }

        if (_parameterOrigins.TryRemove(parameter, out var pool))
        {
            pool.Return(parameter);
        }
    }

    private ObjectPool<DbParameter> CreatePool(DbCommand command)
    {
        var prototype = command.CreateParameter();
        var policy = new DbParameterPooledObjectPolicy(prototype.GetType());
        var pool = _poolProvider.Create(policy);
        pool.Return(prototype);
        return pool;
    }

    private sealed class DbParameterPooledObjectPolicy : PooledObjectPolicy<DbParameter>
    {
        private readonly Type _parameterType;

        public DbParameterPooledObjectPolicy(Type parameterType)
        {
            _parameterType = parameterType ?? throw new ArgumentNullException(nameof(parameterType));
        }

        public override DbParameter Create() => (DbParameter)Activator.CreateInstance(_parameterType)!;

        public override bool Return(DbParameter obj)
        {
            obj.ParameterName = string.Empty;
            obj.Value = DBNull.Value;
            obj.Direction = ParameterDirection.Input;
            obj.IsNullable = false;
            obj.SourceColumn = string.Empty;
            obj.Size = 0;
            obj.Precision = 0;
            obj.Scale = 0;
            obj.ResetDbType();
            return true;
        }
    }
}
