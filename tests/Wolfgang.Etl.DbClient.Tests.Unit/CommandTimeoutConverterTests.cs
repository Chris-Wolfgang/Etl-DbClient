using System;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class CommandTimeoutConverterTests
{
    [Fact]
    public void ToSeconds_when_null_returns_null()
    {
        Assert.Null(CommandTimeoutConverter.ToSeconds(null));
    }



    [Fact]
    public void ToSeconds_when_zero_returns_zero()
    {
        Assert.Equal(0, CommandTimeoutConverter.ToSeconds(TimeSpan.Zero));
    }



    [Theory]
    [InlineData(1, 1)]
    [InlineData(250, 1)]
    [InlineData(500, 1)]
    [InlineData(1000, 1)]
    [InlineData(1001, 2)]
    [InlineData(30000, 30)]
    [InlineData(30500, 31)]
    public void ToSeconds_rounds_up_so_a_positive_timeout_is_never_zero(int milliseconds, int expected)
    {
        Assert.Equal(expected, CommandTimeoutConverter.ToSeconds(TimeSpan.FromMilliseconds(milliseconds)));
    }



    [Fact]
    public void ToSeconds_when_larger_than_int_range_clamps_to_int_MaxValue()
    {
        Assert.Equal(int.MaxValue, CommandTimeoutConverter.ToSeconds(TimeSpan.MaxValue));
    }



    [Fact]
    public void ToSeconds_when_exactly_int_MaxValue_seconds_returns_int_MaxValue()
    {
        Assert.Equal(int.MaxValue, CommandTimeoutConverter.ToSeconds(TimeSpan.FromSeconds(int.MaxValue)));
    }
}
