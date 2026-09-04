using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Direct coverage for the parameter-name comparison. Its behaviour with real names is exercised
/// through <see cref="DbExtractor{TRecord}"/> in EtlParameterBindingTests; the degenerate inputs
/// below are only reachable from here.
/// </summary>
public class ParameterNameTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("@", "")]
    public void Degenerate_names_normalise_to_the_same_empty_name(string? left, string? right)
    {
        // "@" alone normalises to empty, which is why it matches "". Guarding the branch matters:
        // a null or empty key reaching Matches must not throw on the way to a comparison.
        Assert.True(ParameterName.Matches(left, right));
    }



    [Theory]
    [InlineData(null, "PageOffset")]
    [InlineData("", "PageOffset")]
    [InlineData("PageOffset", null)]
    public void A_degenerate_name_does_not_match_a_real_one(string? left, string? right)
    {
        Assert.False(ParameterName.Matches(left, right));
    }
}
