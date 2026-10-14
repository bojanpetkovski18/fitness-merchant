namespace FitnessMerchant.Api.Domain.Carts;

public sealed record CartTotals(decimal Subtotal, decimal Shipping, decimal Total, decimal AmountToFreeShipping);

/// <summary>Cart rules from SPEC section 5. Amounts are decimal and already rounded to cents per line.</summary>
public static class CartPricing
{
    public const int MaxLineQuantity = 10;
    public const decimal ShippingCost = 4.90m;
    public const decimal FreeShippingThreshold = 60.00m;

    /// <summary>What a line may hold: available stock, capped per line.</summary>
    public static int MaxQuantity(int stock) => Math.Clamp(stock, 0, MaxLineQuantity);

    public static decimal LineTotal(decimal unitPrice, int quantity) => unitPrice * quantity;

    /// <summary>Shipping is free from the threshold upwards; an empty cart has no shipping.</summary>
    public static CartTotals Totals(IEnumerable<decimal> lineTotals)
    {
        var subtotal = lineTotals.Sum();
        var shipping = subtotal == 0m || subtotal >= FreeShippingThreshold ? 0m : ShippingCost;
        var toFree = subtotal >= FreeShippingThreshold ? 0m : FreeShippingThreshold - subtotal;
        return new CartTotals(subtotal, shipping, subtotal + shipping, toFree);
    }
}
