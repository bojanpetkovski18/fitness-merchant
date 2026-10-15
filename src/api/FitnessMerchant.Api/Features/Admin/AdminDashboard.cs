using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Admin;

/// <summary>
/// Sales figures for a range of UTC days (D027). Every number is aggregated by PostgreSQL; nothing is summed in C#.
/// Counted orders are Paid, Shipped or Delivered (money was taken and not given back), by the day they were placed.
/// </summary>
public sealed class GetDashboardQuery(AppDbContext db, TimeProvider time)
{
    public const int TopProductCount = 5;
    public const int LowStockThreshold = CatalogMapping.LowStockThreshold;
    public const int LowStockLimit = 20;

    internal static readonly OrderStatus[] CountedStatuses = [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered];

    public async Task<IResult> ExecuteAsync(string? from, string? to, CancellationToken cancellationToken)
    {
        var (range, errors) = DateRange.Parse(from, to, time.GetUtcNow());
        if (range is null)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }

        var counted = db.Orders.AsNoTracking()
            .Where(o => CountedStatuses.Contains(o.Status) && o.PlacedAt >= range.Start && o.PlacedAt < range.EndExclusive);

        var totals = await counted
            .GroupBy(_ => 1)
            .Select(g => new { Revenue = g.Sum(o => o.Total), Count = g.Count() })
            .SingleOrDefaultAsync(cancellationToken);
        var revenue = totals?.Revenue ?? 0m;
        var count = totals?.Count ?? 0;

        var byDay = await RevenueByDayAsync(range, cancellationToken);

        var top = await (
                from item in db.OrderItems
                join order in counted on item.OrderId equals order.Id
                group item by item.ProductId into g
                select new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.LineTotal) })
            .OrderByDescending(t => t.Quantity).ThenByDescending(t => t.Revenue).ThenBy(t => t.ProductId)
            .Take(TopProductCount)
            .Join(db.Products, t => t.ProductId, p => p.Id, (t, p) => new TopProduct(t.ProductId, p.Name, p.Sku, t.Quantity, t.Revenue))
            .ToListAsync(cancellationToken);
        // The join may reorder rows: restore the ranking (full ties by name; C# and PostgreSQL order UUIDs differently).
        top = [.. top.OrderByDescending(t => t.Quantity).ThenByDescending(t => t.Revenue).ThenBy(t => t.Name, StringComparer.Ordinal)];

        var lowStock = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.StockQuantity <= LowStockThreshold)
            .OrderBy(p => p.StockQuantity).ThenBy(p => p.Name)
            .Take(LowStockLimit)
            .Select(p => new LowStockProduct(p.Id, p.Name, p.Sku, p.StockQuantity))
            .ToListAsync(cancellationToken);

        return Results.Ok(new DashboardResponse(
            range.From, range.To, revenue, count,
            count == 0 ? 0m : Math.Round(revenue / count, 2, MidpointRounding.AwayFromZero),
            byDay, top, lowStock));
    }

    /// <summary>
    /// One row per day of the range, zero when nothing sold: <c>generate_series</c> left-joined to the counted
    /// orders. The dates are SQL parameters (interpolated <c>SqlQuery</c>), the statuses are fixed literals.
    /// </summary>
    private async Task<List<DailyRevenue>> RevenueByDayAsync(DateRange range, CancellationToken cancellationToken)
    {
        var from = range.From;
        var to = range.To;
        var rows = await db.Database.SqlQuery<DayRow>($"""
            SELECT d.day::date AS day,
                COALESCE(SUM(o.total), 0) AS revenue,
                COUNT(o.id)::int AS orders
            FROM generate_series({from}::date, {to}::date, interval '1 day') AS d(day)
            LEFT JOIN orders o
                ON o.placed_at >= d.day AT TIME ZONE 'UTC'
                AND o.placed_at < (d.day + interval '1 day') AT TIME ZONE 'UTC'
                AND o.status IN ('Paid', 'Shipped', 'Delivered')
            GROUP BY d.day
            ORDER BY d.day
            """).ToListAsync(cancellationToken);
        return rows.Select(r => new DailyRevenue(r.Day, r.Revenue, r.Orders)).ToList();
    }

    /// <summary>Column names follow the snake_case convention: day, revenue, orders.</summary>
    internal sealed class DayRow
    {
        public DateOnly Day { get; init; }
        public decimal Revenue { get; init; }
        public int Orders { get; init; }
    }
}
