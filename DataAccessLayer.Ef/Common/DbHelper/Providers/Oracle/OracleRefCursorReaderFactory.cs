using System;
using System.Data.Common;
using DataAccessLayer.Exceptions;

namespace DataAccessLayer.Providers.Oracle;

/// <summary>
/// Converts Oracle REF CURSOR parameter values into <see cref="DbDataReader"/> instances using reflection.
/// </summary>
public static class OracleRefCursorReaderFactory
{
    /// <summary>
    /// Creates a <see cref="DbDataReader"/> from the supplied REF CURSOR parameter.
    /// </summary>
    /// <param name="parameter">Oracle parameter whose value is a REF CURSOR.</param>
    /// <returns>A live <see cref="DbDataReader"/> returned by the provider.</returns>
    /// <exception cref="ProviderFeatureException">Thrown when the parameter value is null or does not expose GetDataReader().</exception>
    public static DbDataReader Create(DbParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        if (parameter.Value is null)
        {
            throw new ProviderFeatureException("REF CURSOR parameter value is null.");
        }

        var method = parameter.Value.GetType().GetMethod("GetDataReader", Type.EmptyTypes);
        if (method is null)
        {
            throw new ProviderFeatureException("REF CURSOR parameter does not expose GetDataReader().");
        }

        if (method.Invoke(parameter.Value, null) is not DbDataReader reader)
        {
            throw new ProviderFeatureException("REF CURSOR GetDataReader() did not return a DbDataReader.");
        }

        return reader;
    }
}
