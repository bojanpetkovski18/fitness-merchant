namespace FitnessMerchant.Api.Domain.Orders;

public sealed class Order
{
    public Guid Id { get; init; }

    /// <summary>Human-readable and sequential, e.g. FM-000123.</summary>
    public required string Number { get; init; }

    public Guid UserId { get; init; }
    public OrderStatus Status { get; private set; }

    public decimal Subtotal { get; init; }
    public decimal ShippingCost { get; init; }
    public decimal Total { get; init; }
    public string Currency { get; init; } = "EUR";

    public required ShippingAddress Shipping { get; init; }
    public required string ContactEmail { get; init; }

    public required string PaymentProvider { get; init; }
    public string? PaymentReference { get; private set; }
    public string? CardBrand { get; private set; }
    public string? CardLast4 { get; private set; }

    /// <summary>Client-generated key that makes a repeated "Place order" return the same order.</summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>Why the order was cancelled, e.g. "payment-declined" or "customer"; null otherwise.</summary>
    public string? CancellationReason { get; private set; }

    public DateTimeOffset PlacedAt { get; init; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? ShippedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    public uint Version { get; private set; }

    public ICollection<OrderItem> Items { get; } = [];
    public ICollection<OrderStatusChange> StatusChanges { get; } = [];

    /// <summary>A new order waits for payment; the first audit row records that.</summary>
    public static Order Place(
        Guid id, string number, Guid userId, IEnumerable<OrderItem> items, decimal subtotal, decimal shipping,
        ShippingAddress address, string contactEmail, string paymentProvider, string idempotencyKey, DateTimeOffset now)
    {
        var order = new Order
        {
            Id = id,
            Number = number,
            UserId = userId,
            Status = OrderStatus.PendingPayment,
            Subtotal = subtotal,
            ShippingCost = shipping,
            Total = subtotal + shipping,
            Shipping = address,
            ContactEmail = contactEmail,
            PaymentProvider = paymentProvider,
            IdempotencyKey = idempotencyKey,
            PlacedAt = now,
        };
        foreach (var item in items)
        {
            order.Items.Add(item);
        }
        order.StatusChanges.Add(new OrderStatusChange
        {
            Id = Guid.CreateVersion7(),
            OrderId = id,
            FromStatus = null,
            ToStatus = OrderStatus.PendingPayment,
            ChangedByUserId = userId,
            ChangedAt = now,
        });
        return order;
    }

    public void MarkPaid(string reference, string cardBrand, string cardLast4, DateTimeOffset at)
    {
        TransitionTo(OrderStatus.Paid, at, changedBy: null, note: null);
        PaymentReference = reference;
        CardBrand = cardBrand;
        CardLast4 = cardLast4;
    }

    /// <summary>Caller restocks; the aggregate only records the decision.</summary>
    public void Cancel(string reason, DateTimeOffset at, Guid? changedBy, string? note = null)
    {
        TransitionTo(OrderStatus.Cancelled, at, changedBy, note);
        CancellationReason = reason;
    }

    public void TransitionTo(OrderStatus to, DateTimeOffset at, Guid? changedBy, string? note)
    {
        if (!OrderStateMachine.CanTransition(Status, to))
        {
            throw new InvalidOrderTransitionException(Status, to);
        }

        StatusChanges.Add(new OrderStatusChange
        {
            Id = Guid.CreateVersion7(),
            OrderId = Id,
            FromStatus = Status,
            ToStatus = to,
            ChangedByUserId = changedBy,
            ChangedAt = at,
            Note = note,
        });
        Status = to;
        switch (to)
        {
            case OrderStatus.Paid: PaidAt = at; break;
            case OrderStatus.Shipped: ShippedAt = at; break;
            case OrderStatus.Delivered: DeliveredAt = at; break;
            case OrderStatus.Cancelled: CancelledAt = at; break;
        }
    }
}

/// <summary>Snapshot of what was bought, at the price paid; independent of later catalog changes.</summary>
public sealed class OrderItem
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required string ProductSlug { get; init; }
    public required string Sku { get; init; }
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal LineTotal { get; init; }
}

public sealed class OrderStatusChange
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public OrderStatus? FromStatus { get; init; }
    public OrderStatus ToStatus { get; init; }
    public Guid? ChangedByUserId { get; init; }
    public DateTimeOffset ChangedAt { get; init; }
    public string? Note { get; init; }
}

public sealed record ShippingAddress(
    string FullName, string Line1, string? Line2, string City, string PostalCode, string Country, string Phone);
