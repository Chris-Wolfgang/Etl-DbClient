using Xunit;

namespace Wolfgang.Etl.Ado.Tests.Unit;

public class AdoReportTests
{
    [Fact]
    public void Constructor_sets_all_properties()
    {
        var report = new AdoReport(10, 2, "SELECT 1", 500);

        Assert.Equal(10, report.CurrentItemCount);
        Assert.Equal(2, report.CurrentSkippedItemCount);
        Assert.Equal("SELECT 1", report.CommandText);
        Assert.Equal(500, report.ElapsedMilliseconds);
    }



    [Fact]
    public void Equals_with_same_values_returns_true()
    {
        var a = new AdoReport(10, 2, "SELECT 1", 500);
        var b = new AdoReport(10, 2, "SELECT 1", 500);

        Assert.Equal(a, b);
    }



    [Fact]
    public void Equals_with_different_values_returns_false()
    {
        var a = new AdoReport(10, 2, "SELECT 1", 500);
        var b = new AdoReport(10, 2, "SELECT 1", 999);

        Assert.NotEqual(a, b);
    }



    [Fact]
    public void GetHashCode_with_same_values_returns_same_hash()
    {
        var a = new AdoReport(10, 2, "SELECT 1", 500);
        var b = new AdoReport(10, 2, "SELECT 1", 500);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }



    [Fact]
    public void ToString_contains_property_values()
    {
        var report = new AdoReport(10, 2, "SELECT 1", 500);
        var str = report.ToString();

        Assert.Contains("SELECT 1", str, System.StringComparison.Ordinal);
    }
}
