namespace FitnessMerchant.Api.Domain.Catalog;

/// <summary>Price rules from SPEC section 5. Money is decimal; rounding is half away from zero to 2 places.</summary>
public static class Pricing
{
    public const int MaxDiscountPercent = 90;

    public static decimal SalePrice(decimal price, int discountPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        ArgumentOutOfRangeException.ThrowIfNegative(discountPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(discountPercent, MaxDiscountPercent);

        return Math.Round(price * (100 - discountPercent) / 100m, 2, MidpointRounding.AwayFromZero);
    }
}
