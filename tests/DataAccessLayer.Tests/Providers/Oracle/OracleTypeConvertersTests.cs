using DataAccessLayer.Providers.Oracle;
using Oracle.ManagedDataAccess.Types;
using Xunit;

namespace DataAccessLayer.Tests.Providers.Oracle;

public sealed class OracleTypeConvertersTests
{
    [Fact]
    public void ToInt32_OracleDecimal()
    {
        var number = new OracleDecimal(5);
        Assert.Equal(5, OracleTypeConverters.ToInt32(number));
    }

    [Fact]
    public void ToDecimal_OracleDecimal()
    {
        var number = new OracleDecimal(12.5m);
        Assert.Equal(12.5m, OracleTypeConverters.ToDecimal(number));
    }
}
