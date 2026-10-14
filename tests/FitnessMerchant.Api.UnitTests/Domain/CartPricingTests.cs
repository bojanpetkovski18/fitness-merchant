using FitnessMerchant.Api.Domain.Carts;

namespace FitnessMerchant.Api.UnitTests.Domain;

public class CartPricingTests
{
    [Theory]
    [InlineData("59.99", "4.90", "64.89", "0.01")]
    [InlineData("60.00", "0", "60.00", "0")]
    [InlineData("60.01", "0", "60.01", "0")]
    [InlineData("0.01", "4.90", "4.91", "59.99")]
    public void Shipping_is_free_from_60_euros(string subtotal, string shipping, string total, string toFree)
    {
        var totals = CartPricing.Totals([D(subtotal)]);

        Assert.Equal(new CartTotals(D(subtotal), D(shipping), D(total), D(toFree)), totals);
    }

    [Fact]
    public void An_empty_cart_costs_nothing_and_has_no_shipping()
    {
        Assert.Equal(new CartTotals(0m, 0m, 0m, 60m), CartPricing.Totals([]));
    }

    [Fact]
    public void Subtotal_sums_line_totals_exactly()
    {
        var lines = new[] { CartPricing.LineTotal(53.91m, 1), CartPricing.LineTotal(3.90m, 3), CartPricing.LineTotal(0.07m, 3) };

        var totals = CartPricing.Totals(lines);

        Assert.Equal(65.82m, totals.Subtotal);
        Assert.Equal(0m, totals.Shipping);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-3, 0)]
    [InlineData(3, 3)]
    [InlineData(10, 10)]
    [InlineData(42, 10)]
    public void A_line_holds_at_most_stock_capped_at_ten(int stock, int expected) =>
        Assert.Equal(expected, CartPricing.MaxQuantity(stock));

    private static decimal D(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
