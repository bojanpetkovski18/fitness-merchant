using FitnessMerchant.Api.Domain.Carts;
using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Data.Configurations;
using FitnessMerchant.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Infrastructure.Seed;

/// <summary>
/// Order history for the dashboard and demo (M06): fictional customers (no password, so they cannot sign in)
/// and about 40 orders over the last 90 days in realistic states, each with its audit trail. Deterministic
/// (fixed random seed), dates relative to now, numbers from the real sequence in date order. Runs once:
/// skipped when any order exists. Stock is not touched (these are history, not new sales).
/// </summary>
public sealed partial class OrderSeeder(AppDbContext db, TimeProvider time, ILogger<OrderSeeder> logger)
{
    public const int OrderCount = 40;

    private static readonly (string Name, string City, string Country)[] Customers =
    [
        ("Ana Petrova", "Skopje", "North Macedonia"),
        ("Luka Novak", "Ljubljana", "Slovenia"),
        ("Mila Hansen", "Aarhus", "Denmark"),
        ("Jonas Weber", "Leipzig", "Germany"),
        ("Sara Rossi", "Bologna", "Italy"),
        ("Tomas Kral", "Krakow", "Poland"),
        ("Ines Duarte", "Porto", "Portugal"),
        ("Erik Lund", "Uppsala", "Sweden"),
        ("Nina Kovac", "Split", "Croatia"),
        ("Marco Bianchi", "Graz", "Austria"),
    ];

    public async Task<bool> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Orders.AnyAsync(cancellationToken))
        {
            LogSkipped(logger);
            return false;
        }

        var products = await db.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Sku)
            .Select(p => new { p.Id, p.Name, p.Slug, p.Sku, SalePrice = Math.Round(p.Price * (100 - p.DiscountPercent) / 100m, 2) })
            .ToListAsync(cancellationToken);
        if (products.Count == 0)
        {
            return false; // the catalog seed runs first
        }

        var now = time.GetUtcNow();
        var random = new Random(20261014);
        var customerRole = Roles.All.Single(r => r.Name == Roles.Customer).Id;

        await using var transaction = await JoinableTransaction.BeginAsync(db, cancellationToken);

        var users = Customers.Select((c, index) =>
        {
            var email = $"{Slug(c.Name)}@customers.example";
            return (Customer: c, Index: index, User: new AppUser
            {
                Id = SeedIds.For($"user:{email}"),
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                DisplayName = c.Name,
                CreatedAt = now.AddDays(-120 - index),
            });
        }).ToList();
        // Reuse seed customers that already exist (e.g. after an order reset), so re-seeding cannot collide.
        var existing = await db.Users.Where(u => users.Select(x => x.User.Id).Contains(u.Id)).Select(u => u.Id).ToListAsync(cancellationToken);
        var newUsers = users.Where(u => !existing.Contains(u.User.Id)).Select(u => u.User).ToList();
        db.Users.AddRange(newUsers);
        db.UserRoles.AddRange(newUsers.Select(u => new IdentityUserRole<Guid> { UserId = u.Id, RoleId = customerRole }));

        // Draw the orders first, then number them in date order so FM-000001 is the oldest.
        var drafts = Enumerable.Range(0, OrderCount).Select(i =>
        {
            // The newest few are recent enough to still await shipping, so staff have orders to process.
            var daysAgo = i >= OrderCount - 6 ? random.Next(0, 2) : random.Next(3, 90);
            var placedAt = now.AddDays(-daysAgo).AddMinutes(-random.Next(30, 20 * 60));
            var picks = products.OrderBy(_ => random.Next()).Take(random.Next(1, 4)).ToList();
            // Nobody buys three treadmills: big-ticket items come one at a time.
            var quantities = picks.Select(p => p.SalePrice > 200m ? 1 : random.Next(1, 4)).ToList();
            return (Index: i, PlacedAt: placedAt, Picks: picks, Quantities: quantities, Customer: users[i % users.Count], Roll: random.NextDouble());
        }).OrderBy(d => d.PlacedAt).ToList();

        foreach (var draft in drafts)
        {
            var number = (await db.Database
                .SqlQuery<long>($"SELECT nextval({OrderConfiguration.NumberSequence}::regclass) AS \"Value\"")
                .ToListAsync(cancellationToken)).Single();
            var orderId = SeedIds.For($"order:{draft.Index}");
            var items = draft.Picks.Select((p, k) => new OrderItem
            {
                Id = SeedIds.For($"order:{draft.Index}:item:{k}"),
                OrderId = orderId,
                ProductId = p.Id,
                ProductName = p.Name,
                ProductSlug = p.Slug,
                Sku = p.Sku,
                UnitPrice = p.SalePrice,
                Quantity = draft.Quantities[k],
                LineTotal = CartPricing.LineTotal(p.SalePrice, draft.Quantities[k]),
            }).ToList();
            var totals = CartPricing.Totals(items.Select(i => i.LineTotal));
            var (customer, index, user) = draft.Customer;

            var order = Order.Place(
                orderId, $"FM-{number:D6}", user.Id, items, totals.Subtotal, totals.Shipping,
                new ShippingAddress(customer.Name, $"{10 + index} Example Street", null, customer.City, $"{1000 + index * 7}",
                    customer.Country, $"+00 000 000 {index:D3}"),
                user.Email!, Payments.PaymentOptions.Fake, $"seed-{draft.Index}", draft.PlacedAt);

            var paidAt = draft.PlacedAt.AddMinutes(2);
            order.MarkPaid($"fake_seed_{draft.Index}", "Visa", "4242", paidAt);

            var ageDays = (now - draft.PlacedAt).TotalDays;
            if (draft.Roll < 0.1)
            {
                order.Cancel("customer", paidAt.AddHours(3), user.Id, "Cancelled by the customer.");
            }
            else if (ageDays > 3)
            {
                var shippedAt = draft.PlacedAt.AddDays(1).AddHours(random.Next(0, 12));
                order.TransitionTo(OrderStatus.Shipped, shippedAt, changedBy: null, note: null);
                var deliveredAt = shippedAt.AddDays(random.Next(2, 5));
                if (ageDays > 8 && deliveredAt < now)
                {
                    order.TransitionTo(OrderStatus.Delivered, deliveredAt, changedBy: null, note: null);
                }
            }
            db.Orders.Add(order);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogSeeded(logger, users.Count, drafts.Count);
        return true;
    }

    private static string Slug(string name) => name.ToLowerInvariant().Replace(' ', '.');

    [LoggerMessage(Level = LogLevel.Information, Message = "Orders already exist; order seeding skipped.")]
    private static partial void LogSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Customers} customers and {Orders} historical orders.")]
    private static partial void LogSeeded(ILogger logger, int customers, int orders);
}
