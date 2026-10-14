using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Orders;

/// <summary>The caller's own orders only; someone else's order number answers exactly like a missing one (404).</summary>
public sealed class OrderQueries(AppDbContext db)
{
    public const int PageSize = 10;

    public async Task<PagedResponse<OrderSummaryResponse>> ListAsync(Guid userId, int page, CancellationToken cancellationToken)
    {
        var mine = db.Orders.AsNoTracking().Where(o => o.UserId == userId);
        var total = await mine.CountAsync(cancellationToken);
        var items = await mine
            .OrderByDescending(o => o.PlacedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(o => new OrderSummaryResponse(o.Number, o.Status, o.PlacedAt, o.Total, o.Items.Sum(i => i.Quantity)))
            .ToListAsync(cancellationToken);
        return new PagedResponse<OrderSummaryResponse>(items, page, PageSize, total);
    }

    public async Task<OrderResponse?> GetAsync(Guid userId, string number, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).Include(o => o.StatusChanges)
            .AsSplitQuery()
            .SingleOrDefaultAsync(o => o.UserId == userId && o.Number == number, cancellationToken);
        return order?.ToResponse(await LiveSlugsAsync(db, order, cancellationToken));
    }

    /// <summary>Current slugs of the order's products that are still on sale, in one query.</summary>
    internal static async Task<Dictionary<Guid, string>> LiveSlugsAsync(AppDbContext db, Order order, CancellationToken cancellationToken)
    {
        var ids = order.Items.Select(i => i.ProductId).ToList();
        return await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.IsActive)
            .ToDictionaryAsync(p => p.Id, p => p.Slug, cancellationToken);
    }
}

public sealed class OrderCommands(AppDbContext db, TimeProvider time)
{
    /// <summary>Customers may cancel until the order ships; the stock goes back to the shelf.</summary>
    public async Task<IResult> CancelAsync(Guid userId, string number, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Items).Include(o => o.StatusChanges)
            .AsSplitQuery()
            .SingleOrDefaultAsync(o => o.UserId == userId && o.Number == number, cancellationToken);
        if (order is null)
        {
            return Results.NotFound();
        }
        if (!OrderStateMachine.CustomerCanCancel(order.Status))
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "This order can no longer be cancelled.",
                extensions: new Dictionary<string, object?> { ["code"] = "not-cancellable" });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = time.GetUtcNow();
        order.Cancel("customer", now, changedBy: userId, note: "Cancelled by the customer.");
        foreach (var item in order.Items)
        {
            await StockLedger.ReturnAsync(db, item.ProductId, item.Quantity, now, cancellationToken);
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Changed meanwhile (e.g. shipped by staff); the restock above is rolled back with the transaction.
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "This order changed meanwhile. Reload and try again.",
                extensions: new Dictionary<string, object?> { ["code"] = "concurrent-change" });
        }
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(order.ToResponse(await OrderQueries.LiveSlugsAsync(db, order, cancellationToken)));
    }
}
