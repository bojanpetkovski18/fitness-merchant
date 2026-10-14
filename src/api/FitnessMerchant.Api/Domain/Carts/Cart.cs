namespace FitnessMerchant.Api.Domain.Carts;

/// <summary>A customer's server-side cart; one per user, created on the first write.</summary>
public sealed class Cart
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<CartItem> Items { get; } = [];
}

public sealed class CartItem
{
    public Guid CartId { get; init; }
    public Guid ProductId { get; init; }
    public int Quantity { get; set; }
    public DateTimeOffset AddedAt { get; init; }
}
