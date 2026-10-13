using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Infrastructure.Seed;

namespace FitnessMerchant.Api.UnitTests.Common;

public class SlugsAndSeedIdsTests
{
    [Theory]
    [InlineData("Whey Isolate Vanilla 2 kg", "whey-isolate-vanilla-2-kg")]
    [InlineData("Vitamin D3 + K2 (90 caps)", "vitamin-d3-k2-90-caps")]
    [InlineData("Adjustable Dumbbell Pair 2–24 kg", "adjustable-dumbbell-pair-2-24-kg")]
    [InlineData("  Café   Crème  ", "cafe-creme")]
    [InlineData("Brakken Barbell Co.", "brakken-barbell-co")]
    [InlineData("---", "")]
    public void Slugify_produces_lowercase_ascii_with_single_hyphens(string input, string expected) =>
        Assert.Equal(expected, Slugs.Slugify(input));

    [Fact]
    public void Seed_ids_are_stable_distinct_and_version_8()
    {
        var first = SeedIds.For("product:a");

        Assert.Equal(first, SeedIds.For("product:a"));
        Assert.NotEqual(first, SeedIds.For("product:b"));
        Assert.Equal(8, first.Version);
        Assert.Equal('8', first.ToString()[14]);
    }
}
