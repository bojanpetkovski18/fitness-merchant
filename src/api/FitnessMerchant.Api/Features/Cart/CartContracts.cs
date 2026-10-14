using System.ComponentModel.DataAnnotations;

namespace FitnessMerchant.Api.Features.Cart;

/// <summary>Why a line cannot be bought as it is. Serialized as "unavailable", "out-of-stock", "exceeds-stock".</summary>
public enum CartLineIssue
{
    /// <summary>The product was archived; the line is excluded from totals.</summary>
    Unavailable,

    /// <summary>No stock left; the line is excluded from totals.</summary>
    OutOfStock,

    /// <summary>Less stock than the line asks for; totals use <c>MaxQuantity</c>.</summary>
    ExceedsStock,
}

/// <param name="Quantity">What the customer asked for.</param>
/// <param name="MaxQuantity">What can be bought now: stock capped at 10, or 0 if unavailable.</param>
/// <param name="LineTotal">Unit (sale) price times the quantity that can actually be bought.</param>
public sealed record CartLineResponse(
    Guid ProductId,
    string Slug,
    string Name,
    string BrandName,
    string? ImageUrl,
    decimal Price,
    int DiscountPercent,
    decimal UnitPrice,
    int Quantity,
    int MaxQuantity,
    decimal LineTotal,
    CartLineIssue? Issue);

public sealed record CartResponse(
    IReadOnlyList<CartLineResponse> Lines,
    int ItemCount,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    decimal FreeShippingThreshold,
    decimal AmountToFreeShipping);

public sealed record SetQuantityRequest([property: Range(1, 10)] int Quantity);

/// <summary>Guest carts may be stale or edited by hand, so quantities are capped rather than rejected.</summary>
public sealed record CartItemInput(Guid ProductId, [property: Range(1, 99)] int Quantity);

public sealed record CartItemsRequest([property: Required, MaxLength(50)] IReadOnlyList<CartItemInput> Items);
