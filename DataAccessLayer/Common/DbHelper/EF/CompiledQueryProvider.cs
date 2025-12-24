using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.EF;

/// <summary>
/// Caches compiled EF Core queries keyed by an application-provided identifier.
/// </summary>
public static class CompiledQueryProvider
{
    private static readonly ConcurrentDictionary<string, Delegate> Cache = new();
    private static readonly ConcurrentDictionary<string, object> ProfiledCache = new();

    public static Func<TContext, TResult> GetOrAdd<TContext, TResult>(string key, Expression<Func<TContext, TResult>> query)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(query);
        return (Func<TContext, TResult>)Cache.GetOrAdd(BuildKey<TContext>(key), _ => Microsoft.EntityFrameworkCore.EF.CompileQuery(query));
    }

    public static Func<TContext, TArg, TResult> GetOrAdd<TContext, TArg, TResult>(
        string key,
        Expression<Func<TContext, TArg, TResult>> query)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(query);
        return (Func<TContext, TArg, TResult>)Cache.GetOrAdd(BuildKey<TContext>(key), _ => Microsoft.EntityFrameworkCore.EF.CompileQuery(query));
    }

    public static CompiledQueryHandle<TContext, TResult> GetOrAdd<TContext, TResult>(
        CompiledQueryDescriptor descriptor,
        Expression<Func<TContext, TResult>> query)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = BuildProfiledKey<TContext, TResult>(descriptor.Key);
        return (CompiledQueryHandle<TContext, TResult>)ProfiledCache.GetOrAdd(
            cacheKey,
            _ => new CompiledQueryHandle<TContext, TResult>(
                Microsoft.EntityFrameworkCore.EF.CompileQuery(query),
                descriptor.ParameterProfile));
    }

    public static CompiledQueryHandle<TContext, TArg, TResult> GetOrAdd<TContext, TArg, TResult>(
        CompiledQueryDescriptor descriptor,
        Expression<Func<TContext, TArg, TResult>> query)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = BuildProfiledKey<TContext, TResult>(descriptor.Key, typeof(TArg));
        return (CompiledQueryHandle<TContext, TArg, TResult>)ProfiledCache.GetOrAdd(
            cacheKey,
            _ => new CompiledQueryHandle<TContext, TArg, TResult>(
                Microsoft.EntityFrameworkCore.EF.CompileQuery(query),
                descriptor.ParameterProfile));
    }

    private static string BuildKey<TContext>(string key) => $"{typeof(TContext).FullName}:{key}";

    private static string BuildProfiledKey<TContext, TResult>(string key, Type? argType = null) =>
        $"{typeof(TContext).FullName}:{typeof(TResult).FullName}:{argType?.FullName}:{key}:profiled";
}
