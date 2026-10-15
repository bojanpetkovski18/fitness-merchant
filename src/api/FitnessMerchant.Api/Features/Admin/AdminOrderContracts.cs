using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Features.Orders;

namespace FitnessMerchant.Api.Features.Admin;

public sealed record AdminOrderRow(
    string Number,
    OrderStatus Status,
    DateTimeOffset PlacedAt,
    string CustomerName,
    string ContactEmail,
    int ItemCount,
    decimal Total);

public sealed record AdminCustomer(Guid Id, string Email, string DisplayName);

public sealed record AdminStatusChange(OrderStatus? FromStatus, OrderStatus ToStatus, DateTimeOffset At, string? Note, string? ChangedBy);

/// <summary><c>AllowedTransitions</c> comes from the state machine, so the UI never offers a move the server would refuse.</summary>
public sealed record AdminOrderResponse(
    OrderResponse Order,
    AdminCustomer Customer,
    IReadOnlyList<OrderStatus> AllowedTransitions,
    IReadOnlyList<AdminStatusChange> History);

public sealed record ChangeStatusRequest(OrderStatus? ToStatus, string? Note);

public sealed record DailyRevenue(DateOnly Day, decimal Revenue, int Orders);

public sealed record TopProduct(Guid ProductId, string Name, string Sku, int Quantity, decimal Revenue);

public sealed record LowStockProduct(Guid ProductId, string Name, string Sku, int StockQuantity);

/// <summary>Revenue counts orders that were paid and not cancelled (Paid, Shipped, Delivered), by the day they were placed (UTC).</summary>
public sealed record DashboardResponse(
    DateOnly From,
    DateOnly To,
    decimal Revenue,
    int OrderCount,
    decimal AverageOrderValue,
    IReadOnlyList<DailyRevenue> RevenueByDay,
    IReadOnlyList<TopProduct> TopProducts,
    IReadOnlyList<LowStockProduct> LowStock);
