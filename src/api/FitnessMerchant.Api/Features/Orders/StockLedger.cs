using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Orders;

/// <summary>
/// Stock changes as single SQL statements, so they are safe under concurrency without application locks.
/// Taking stock is <c>UPDATE … SET stock = stock - n WHERE id = … AND stock &gt;= n</c>: under READ COMMITTED,
/// a competing transaction blocks on the row lock and re-checks the condition after the first commits, so two
/// buyers can never both take the last unit (D024). The <c>stock &gt;= 0</c> check constraint is a second net.
/// </summary>
public static class StockLedger
{
    /// <returns>False when the product is archived or has fewer than <paramref name="quantity"/> left.</returns>
    public static async Task<bool> TryTakeAsync(AppDbContext db, Guid productId, int quantity, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.Products
            .Where(p => p.Id == productId && p.IsActive && p.StockQuantity >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity)
                .SetProperty(p => p.UpdatedAt, now), cancellationToken) == 1;

    public static Task ReturnAsync(AppDbContext db, Guid productId, int quantity, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity)
                .SetProperty(p => p.UpdatedAt, now), cancellationToken);
}
