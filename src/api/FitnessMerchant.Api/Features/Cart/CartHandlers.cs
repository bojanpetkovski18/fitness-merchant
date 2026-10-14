using FitnessMerchant.Api.Domain.Carts;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Cart;

/// <summary>
/// Prices a list of (product, quantity) against current catalog data in one query. Used for the stored cart
/// and for guest quotes, so both follow exactly the same rules.
/// </summary>
public sealed class CartPricer(AppDbContext db)
{
    public async Task<CartResponse> PriceAsync(IReadOnlyList<(Guid ProductId, int Quantity)> items, CancellationToken cancellationToken)
    {
        var ids = items.Select(i => i.ProductId).Distinct().ToArray();
        var products = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id, p.Slug, p.Name, BrandName = p.Brand.Name, p.ImageId, p.Price, p.DiscountPercent,
                SalePrice = Math.Round(p.Price * (100 - p.DiscountPercent) / 100m, 2),
                p.StockQuantity, p.IsActive,
            })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var lines = new List<CartLineResponse>();
        foreach (var (productId, quantity) in items)
        {
            if (!products.TryGetValue(productId, out var p))
            {
                continue; // deleted product: nothing to show
            }

            var max = p.IsActive ? CartPricing.MaxQuantity(p.StockQuantity) : 0;
            CartLineIssue? issue = !p.IsActive ? CartLineIssue.Unavailable
                : max == 0 ? CartLineIssue.OutOfStock
                : quantity > max ? CartLineIssue.ExceedsStock
                : null;
            var buyable = issue is CartLineIssue.Unavailable or CartLineIssue.OutOfStock ? 0 : Math.Min(quantity, max);

            lines.Add(new CartLineResponse(
                p.Id, p.Slug, p.Name, p.BrandName, CatalogMapping.ImageUrl(p.ImageId), p.Price, p.DiscountPercent,
                p.SalePrice, quantity, max, CartPricing.LineTotal(p.SalePrice, buyable), issue));
        }

        var totals = CartPricing.Totals(lines.Select(l => l.LineTotal));
        var itemCount = lines.Sum(l => l.Issue is CartLineIssue.Unavailable or CartLineIssue.OutOfStock ? 0 : Math.Min(l.Quantity, l.MaxQuantity));
        return new CartResponse(lines, itemCount, totals.Subtotal, totals.Shipping, totals.Total,
            CartPricing.FreeShippingThreshold, totals.AmountToFreeShipping);
    }

    /// <summary>Repeated product IDs are combined, keeping first-seen order.</summary>
    public static List<(Guid ProductId, int Quantity)> Combine(IEnumerable<CartItemInput> items) =>
        items.GroupBy(i => i.ProductId).Select(g => (g.Key, g.Sum(i => i.Quantity))).ToList();
}

public sealed class CartQueries(AppDbContext db, CartPricer pricer)
{
    public async Task<CartResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await db.CartItems.AsNoTracking()
            .Where(i => db.Carts.Any(c => c.Id == i.CartId && c.UserId == userId))
            .OrderBy(i => i.AddedAt).ThenBy(i => i.ProductId)
            .Select(i => new { i.ProductId, i.Quantity })
            .ToListAsync(cancellationToken);
        return await pricer.PriceAsync(items.Select(i => (i.ProductId, i.Quantity)).ToList(), cancellationToken);
    }

    public Task<CartResponse> QuoteAsync(CartItemsRequest request, CancellationToken cancellationToken) =>
        pricer.PriceAsync(CartPricer.Combine(request.Items), cancellationToken);
}

/// <summary>
/// Writes to the caller's own cart. Upserts use PostgreSQL ON CONFLICT (parameterized via EF's interpolated SQL),
/// so two tabs adding the same product at once cannot fail on the unique keys.
/// </summary>
public sealed class CartCommands(AppDbContext db, CartQueries queries, TimeProvider time)
{
    public async Task<Results<Ok<CartResponse>, ProblemHttpResult>> SetQuantityAsync(
        Guid userId, Guid productId, int quantity, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new { p.IsActive, p.StockQuantity })
            .SingleOrDefaultAsync(cancellationToken);

        if (product is not { IsActive: true })
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "This product is no longer available.");
        }
        var max = CartPricing.MaxQuantity(product.StockQuantity);
        if (max == 0)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This product is out of stock.",
                extensions: new Dictionary<string, object?> { ["maxQuantity"] = 0 });
        }
        if (quantity > max)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: $"Only {max} available.",
                extensions: new Dictionary<string, object?> { ["maxQuantity"] = max });
        }

        var now = time.GetUtcNow();
        var cartId = await EnsureCartAsync(userId, now, cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO cart_items (cart_id, product_id, quantity, added_at) VALUES ({cartId}, {productId}, {quantity}, {now})
            ON CONFLICT (cart_id, product_id) DO UPDATE SET quantity = EXCLUDED.quantity
            """, cancellationToken);

        return TypedResults.Ok(await queries.GetAsync(userId, cancellationToken));
    }

    public async Task<CartResponse> RemoveAsync(Guid userId, Guid productId, CancellationToken cancellationToken)
    {
        await db.CartItems
            .Where(i => i.ProductId == productId && db.Carts.Any(c => c.Id == i.CartId && c.UserId == userId))
            .ExecuteDeleteAsync(cancellationToken);
        return await queries.GetAsync(userId, cancellationToken);
    }

    public async Task<CartResponse> ClearAsync(Guid userId, CancellationToken cancellationToken)
    {
        await db.CartItems
            .Where(i => db.Carts.Any(c => c.Id == i.CartId && c.UserId == userId))
            .ExecuteDeleteAsync(cancellationToken);
        return await queries.GetAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Adds a guest cart to the stored one after sign-in: quantities are summed and capped at
    /// min(10, stock); unknown, archived and out-of-stock products are skipped (guest data may be stale).
    /// </summary>
    public async Task<CartResponse> MergeAsync(Guid userId, CartItemsRequest request, CancellationToken cancellationToken)
    {
        var items = CartPricer.Combine(request.Items);
        var ids = items.Select(i => i.ProductId).ToArray();
        var stock = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.IsActive && p.StockQuantity > 0)
            .ToDictionaryAsync(p => p.Id, p => p.StockQuantity, cancellationToken);

        var mergeable = items.Where(i => stock.ContainsKey(i.ProductId)).ToList();
        if (mergeable.Count > 0)
        {
            var now = time.GetUtcNow();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var cartId = await EnsureCartAsync(userId, now, cancellationToken);
            foreach (var (productId, quantity) in mergeable)
            {
                var cap = CartPricing.MaxQuantity(stock[productId]);
                var initial = Math.Min(quantity, cap);
                await db.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO cart_items (cart_id, product_id, quantity, added_at) VALUES ({cartId}, {productId}, {initial}, {now})
                    ON CONFLICT (cart_id, product_id) DO UPDATE SET quantity = LEAST(cart_items.quantity + EXCLUDED.quantity, {cap})
                    """, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }

        return await queries.GetAsync(userId, cancellationToken);
    }

    private async Task<Guid> EnsureCartAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = await db.Database.SqlQuery<Guid>(
            $"""
            INSERT INTO carts (id, user_id, updated_at) VALUES ({Guid.CreateVersion7()}, {userId}, {now})
            ON CONFLICT (user_id) DO UPDATE SET updated_at = EXCLUDED.updated_at
            RETURNING id AS "Value"
            """).ToListAsync(cancellationToken);
        return ids.Single();
    }
}
