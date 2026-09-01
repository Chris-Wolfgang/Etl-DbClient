using System;
using System.Data;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class EtlParameterTests
{
    [Fact]
    public void Value_set_in_an_object_initializer_round_trips()
    {
        var p = new EtlParameter<int> { Value = 42 };

        Assert.Equal(42, p.Value);
        Assert.Equal(ParameterDirection.Input, p.Direction);
    }


    [Fact]
    public void Output_parameter_can_be_declared_without_a_value()
    {
        var p = new EtlParameter<int> { DbType = DbType.Int32, Direction = ParameterDirection.Output };

        Assert.Equal(default, p.Value);
        Assert.Equal(ParameterDirection.Output, p.Direction);
        Assert.Equal(DbType.Int32, p.DbType);
    }


    [Fact]
    public void A_generic_parameter_is_reachable_as_the_non_generic_base()
    {
        // parameter binding dispatches on the base, so this must hold for every T
        object boxed = new EtlParameter<string> { Value = "x" };

        Assert.True(boxed is EtlParameter);
    }


    [Fact]
    public void SetValue_stores_a_matching_type_unchanged()
    {
        var p = new EtlParameter<int>();

        p.SetValue(7);

        Assert.Equal(7, p.Value);
    }


    [Fact]
    public void SetValue_converts_a_widened_provider_type()
    {
        // SQLite returns long for INTEGER even when the caller declared int
        var p = new EtlParameter<int>();

        p.SetValue(7L);

        Assert.Equal(7, p.Value);
    }


    [Fact]
    public void SetValue_converts_a_decimal_provider_type()
    {
        // SQL Server returns decimal for NUMERIC
        var p = new EtlParameter<int>();

        p.SetValue(7m);

        Assert.Equal(7, p.Value);
    }


    [Fact]
    public void SetValue_treats_DBNull_as_null()
    {
        var p = new EtlParameter<string> { Value = "before" };

        p.SetValue(DBNull.Value);

        Assert.Null(p.Value);
    }


    [Fact]
    public void SetValue_when_conversion_is_impossible_throws_naming_both_types()
    {
        var p = new EtlParameter<int>();

        var ex = Assert.Throws<InvalidOperationException>(() => p.SetValue("not a number"));

        Assert.Contains("System.String", ex.Message, StringComparison.Ordinal);
        Assert.Contains("System.Int32", ex.Message, StringComparison.Ordinal);
    }


    [Fact]
    public void SetValue_populates_a_nullable_declared_type()
    {
        var p = new EtlParameter<int?>();

        p.SetValue(5L);

        Assert.Equal(5, p.Value);
    }
}
