using System;
using System.Collections.Generic;
using DataAccessLayer.Execution;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.EF;

/// <summary>
/// Represents a compiled query delegate and its accompanying parameter profile.
/// </summary>
public sealed class CompiledQueryHandle<TContext, TResult>
    where TContext : DbContext
{
    internal CompiledQueryHandle(Func<TContext, TResult> execute, IReadOnlyList<DbParameterDefinition> parameterProfile)
    {
        Execute = execute;
        ParameterProfile = parameterProfile;
    }

    /// <summary>
    /// Gets the compiled query delegate.
    /// </summary>
    public Func<TContext, TResult> Execute { get; }

    /// <summary>
    /// Gets the parameter profile cloned from the descriptor.
    /// </summary>
    public IReadOnlyList<DbParameterDefinition> ParameterProfile { get; }

    /// <summary>
    /// Convenience wrapper that invokes <see cref="Execute"/>.
    /// </summary>
    public TResult Invoke(TContext context) => Execute(context);
}

/// <summary>
/// Represents a compiled query delegate that accepts a single argument plus its parameter profile.
/// </summary>
public sealed class CompiledQueryHandle<TContext, TArg, TResult>
    where TContext : DbContext
{
    internal CompiledQueryHandle(Func<TContext, TArg, TResult> execute, IReadOnlyList<DbParameterDefinition> parameterProfile)
    {
        Execute = execute;
        ParameterProfile = parameterProfile;
    }

    /// <summary>
    /// Gets the compiled query delegate.
    /// </summary>
    public Func<TContext, TArg, TResult> Execute { get; }

    /// <summary>
    /// Gets the parameter profile cloned from the descriptor.
    /// </summary>
    public IReadOnlyList<DbParameterDefinition> ParameterProfile { get; }

    /// <summary>
    /// Convenience wrapper that invokes <see cref="Execute"/>.
    /// </summary>
    public TResult Invoke(TContext context, TArg arg) => Execute(context, arg);
}
