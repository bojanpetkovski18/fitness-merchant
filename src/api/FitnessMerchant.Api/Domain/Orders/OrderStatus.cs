namespace FitnessMerchant.Api.Domain.Orders;

public enum OrderStatus
{
    PendingPayment,
    Paid,
    Shipped,
    Delivered,
    Cancelled,
}

/// <summary>SPEC F10: the only allowed status changes. Anything else is rejected.</summary>
public static class OrderStateMachine
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.PendingPayment] = [OrderStatus.Paid, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = [],
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);

    public static IReadOnlyList<OrderStatus> NextStatuses(OrderStatus from) => Allowed[from];

    /// <summary>Customers may cancel until the order ships.</summary>
    public static bool CustomerCanCancel(OrderStatus status) => CanTransition(status, OrderStatus.Cancelled);
}

public sealed class InvalidOrderTransitionException(OrderStatus from, OrderStatus to)
    : InvalidOperationException($"An order cannot go from {from} to {to}.")
{
    public OrderStatus From { get; } = from;
    public OrderStatus To { get; } = to;
}
