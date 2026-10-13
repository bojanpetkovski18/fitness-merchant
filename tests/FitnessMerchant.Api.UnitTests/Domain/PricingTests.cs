using FitnessMerchant.Api.Domain.Catalog;

namespace FitnessMerchant.Api.UnitTests.Domain;

public class PricingTests
{
    [Theory]
    [InlineData("49.90", 0, "49.90")]
    [InlineData("49.90", 10, "44.91")]
    [InlineData("100.00", 90, "10.00")]
    [InlineData("0.00", 50, "0.00")]
    [InlineData("0.01", 50, "0.01")] // 0.005 rounds half away from zero
    [InlineData("10.05", 50, "5.03")] // 5.025 -> 5.03, not banker's 5.02
    [InlineData("19.99", 15, "16.99")] // 16.9915
    [InlineData("1234567890.12", 25, "925925917.59")]
    public void Sale_price_rounds_half_away_from_zero_to_cents(string price, int discount, string expected)
    {
        var result = Pricing.SalePrice(Parse(price), discount);

        Assert.Equal(Parse(expected), result);
        Assert.Equal(2, result.Scale);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(91)]
    public void Discount_outside_0_to_90_is_rejected(int discount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.SalePrice(10m, discount));

    [Fact]
    public void Negative_price_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Pricing.SalePrice(-0.01m, 0));

    private static decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
