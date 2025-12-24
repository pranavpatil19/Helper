using System;
using Oracle.ManagedDataAccess.Types;

namespace DataAccessLayer.Providers.Oracle;

/// <summary>
/// Provides safe conversions for Oracle-specific numeric types.
/// </summary>
public static class OracleTypeConverters
{
    public static int ToInt32(object value)
    {
        if (value is OracleDecimal oracleDecimal)
        {
            return checked((int)oracleDecimal.Value);
        }

        return Convert.ToInt32(value);
    }

    public static long ToInt64(object value)
    {
        if (value is OracleDecimal oracleDecimal)
        {
            return checked((long)oracleDecimal.Value);
        }

        return Convert.ToInt64(value);
    }

    public static decimal ToDecimal(object value)
    {
        if (value is OracleDecimal oracleDecimal)
        {
            return oracleDecimal.Value;
        }

        return Convert.ToDecimal(value);
    }
}
