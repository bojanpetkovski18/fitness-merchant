using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Infrastructure.Payments;

namespace FitnessMerchant.Api.Features.Orders;

public sealed record ShippingInput(
    string? FullName, string? Line1, string? Line2, string? City, string? PostalCode, string? Country, string? Phone, string? Email);

/// <summary>Raw card input from the checkout form; masked in <see cref="ToString"/> so it never reaches logs.</summary>
public sealed record PaymentInput(string? CardNumber, string? Expiry, string? Cvc, string? NameOnCard)
{
    public override string ToString() => "PaymentInput { (redacted) }";
}

/// <param name="ExpectedTotal">The total the customer saw on the review step; a different total is a 409.</param>
public sealed record CheckoutRequest(ShippingInput? Shipping, PaymentInput? Payment, decimal? ExpectedTotal);

public sealed record OrderItemResponse(Guid ProductId, string ProductSlug, string ProductName, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderStatusChangeResponse(OrderStatus Status, DateTimeOffset At, string? Note);

public sealed record PaymentSummary(string Provider, string? CardBrand, string? CardLast4);

public sealed record OrderResponse(
    string Number,
    OrderStatus Status,
    DateTimeOffset PlacedAt,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    string Currency,
    ShippingAddress ShippingAddress,
    string ContactEmail,
    PaymentSummary Payment,
    IReadOnlyList<OrderStatusChangeResponse> History,
    bool CanCancel,
    string? CancellationReason);

public sealed record OrderSummaryResponse(string Number, OrderStatus Status, DateTimeOffset PlacedAt, decimal Total, int ItemCount);

internal static class OrderMapping
{
    public static OrderResponse ToResponse(this Order order) => new(
        order.Number,
        order.Status,
        order.PlacedAt,
        order.Items.Select(i => new OrderItemResponse(i.ProductId, i.ProductSlug, i.ProductName, i.Sku, i.UnitPrice, i.Quantity, i.LineTotal)).ToList(),
        order.Subtotal,
        order.ShippingCost,
        order.Total,
        order.Currency,
        order.Shipping,
        order.ContactEmail,
        new PaymentSummary(order.PaymentProvider, order.CardBrand, order.CardLast4),
        order.StatusChanges.OrderBy(c => c.ChangedAt).ThenBy(c => c.Id).Select(c => new OrderStatusChangeResponse(c.ToStatus, c.ChangedAt, c.Note)).ToList(),
        OrderStateMachine.CustomerCanCancel(order.Status),
        order.CancellationReason);
}
