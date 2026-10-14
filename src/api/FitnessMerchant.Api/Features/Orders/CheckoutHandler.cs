using FitnessMerchant.Api.Domain.Carts;
using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Data.Configurations;
using FitnessMerchant.Api.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FitnessMerchant.Api.Features.Orders;

/// <summary>
/// Turns the caller's cart into an order (SPEC F8–F10):
/// 1. validate input and replay a repeated idempotency key;
/// 2. re-price the cart from current catalog data and reject a changed cart or total;
/// 3. one transaction: create the order with price snapshots, take the stock with a conditional UPDATE
///    (stock &gt;= quantity, so overselling is impossible), clear the cart;
/// 4. charge the gateway outside the transaction; on a decline, cancel, restock and restore the cart.
/// </summary>
public sealed partial class CheckoutHandler(
    AppDbContext db,
    IPaymentGateway gateway,
    TimeProvider time,
    ILogger<CheckoutHandler> logger)
{
    public const string PaymentDeclined = "payment-declined";
    public const string PaymentError = "payment-error";

    public async Task<IResult> ExecuteAsync(Guid userId, string? idempotencyKey, CheckoutRequest request, CancellationToken cancellationToken)
    {
        if (idempotencyKey is null || idempotencyKey.Length is < 8 or > 64 || !idempotencyKey.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "A valid Idempotency-Key header is required.");
        }

        var now = time.GetUtcNow();
        var (address, email, card, errors) = CheckoutValidation.Validate(request, now);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (await LoadOrderAsync(userId, o => o.IdempotencyKey == idempotencyKey, cancellationToken) is { } previous)
        {
            return Replay(previous);
        }

        // 2. Re-price the cart from the catalog as it is now.
        var cartId = await db.Carts.Where(c => c.UserId == userId).Select(c => (Guid?)c.Id).SingleOrDefaultAsync(cancellationToken);
        var lines = await db.CartItems.AsNoTracking()
            .Where(i => i.CartId == cartId)
            .OrderBy(i => i.AddedAt).ThenBy(i => i.ProductId)
            .Join(db.Products, i => i.ProductId, p => p.Id, (i, p) => new
            {
                i.ProductId, i.Quantity, i.AddedAt, p.Name, p.Slug, p.Sku, p.IsActive, p.StockQuantity,
                UnitPrice = Math.Round(p.Price * (100 - p.DiscountPercent) / 100m, 2),
            })
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
        {
            return Problem(StatusCodes.Status400BadRequest, "Your cart is empty.", "cart-empty");
        }
        if (lines.Any(l => !l.IsActive || l.Quantity > CartPricing.MaxQuantity(l.StockQuantity)))
        {
            return Problem(StatusCodes.Status409Conflict, "Some items in your cart changed. Please review your cart.", "cart-changed");
        }

        var items = lines.Select(l => new OrderItem
        {
            Id = Guid.CreateVersion7(),
            ProductId = l.ProductId,
            ProductName = l.Name,
            ProductSlug = l.Slug,
            Sku = l.Sku,
            UnitPrice = l.UnitPrice,
            Quantity = l.Quantity,
            LineTotal = CartPricing.LineTotal(l.UnitPrice, l.Quantity),
        }).ToList();
        var totals = CartPricing.Totals(items.Select(i => i.LineTotal));
        if (request.ExpectedTotal is { } expected && expected != totals.Total)
        {
            return Problem(StatusCodes.Status409Conflict, "Prices changed since you reviewed your order.", "price-changed",
                new Dictionary<string, object?> { ["total"] = totals.Total });
        }

        // 3. Order + stock + cart in one transaction.
        Order order;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var sequence = await db.Database
                .SqlQuery<long>($"SELECT nextval({OrderConfiguration.NumberSequence}::regclass) AS \"Value\"")
                .ToListAsync(cancellationToken);
            var orderId = Guid.CreateVersion7();
            order = Order.Place(orderId, $"FM-{sequence.Single():D6}", userId,
                items.Select(i => new OrderItem
                {
                    Id = i.Id, OrderId = orderId, ProductId = i.ProductId, ProductName = i.ProductName, ProductSlug = i.ProductSlug,
                    Sku = i.Sku, UnitPrice = i.UnitPrice, Quantity = i.Quantity, LineTotal = i.LineTotal,
                }),
                totals.Subtotal, totals.Shipping, address!, email!, gateway.Name, idempotencyKey, now);

            foreach (var line in lines)
            {
                if (!await StockLedger.TryTakeAsync(db, line.ProductId, line.Quantity, now, cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Problem(StatusCodes.Status409Conflict, $"Sorry, {line.Name} just sold out.", "out-of-stock");
                }
            }

            await db.CartItems.Where(i => i.CartId == cartId).ExecuteDeleteAsync(cancellationToken);
            db.Orders.Add(order);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // The same idempotency key raced in from a double-submit; the other request owns the order.
                await transaction.RollbackAsync(cancellationToken);
                return Problem(StatusCodes.Status409Conflict, "This order is already being placed.", "duplicate-submit");
            }
            await transaction.CommitAsync(cancellationToken);
        }

        // 4. Charge outside the transaction (an external call must not hold row locks).
        PaymentResult result;
        try
        {
            result = await gateway.ChargeAsync(new PaymentRequest(order.Id, order.Number, order.Total, order.Currency, card!), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogGatewayError(logger, exception, order.Number);
            await CompensateAsync(order, cartId!.Value, lines.Select(l => (l.ProductId, l.Quantity, l.AddedAt)).ToList(), PaymentError, cancellationToken);
            return Problem(StatusCodes.Status502BadGateway, "The payment could not be processed. Nothing was charged.", PaymentError);
        }

        switch (result)
        {
            case PaymentResult.Succeeded paid:
                order.MarkPaid(paid.Reference, paid.CardBrand, paid.CardLast4, time.GetUtcNow());
                await db.SaveChangesAsync(cancellationToken);
                LogPlaced(logger, order.Number, order.Total);
                return Results.Created($"/api/orders/{order.Number}", order.ToResponse());

            case PaymentResult.Declined declined:
                await CompensateAsync(order, cartId!.Value, lines.Select(l => (l.ProductId, l.Quantity, l.AddedAt)).ToList(), PaymentDeclined, cancellationToken);
                LogDeclined(logger, order.Number);
                return Problem(StatusCodes.Status402PaymentRequired, declined.Reason, PaymentDeclined,
                    new Dictionary<string, object?> { ["detail"] = "No money was taken. Your cart has been kept." });

            default:
                throw new InvalidOperationException($"Unknown payment result {result.GetType().Name}.");
        }
    }

    /// <summary>Undo step 3 after a failed payment: cancel, give the stock back, put the items back in the cart.</summary>
    private async Task CompensateAsync(
        Order order, Guid cartId, List<(Guid ProductId, int Quantity, DateTimeOffset AddedAt)> lines, string reason, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        order.Cancel(reason, now, changedBy: null, note: reason == PaymentDeclined ? "Card declined." : "Payment error.");
        foreach (var (productId, quantity, addedAt) in lines)
        {
            await StockLedger.ReturnAsync(db, productId, quantity, now, cancellationToken);
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO cart_items (cart_id, product_id, quantity, added_at) VALUES ({cartId}, {productId}, {quantity}, {addedAt})
                ON CONFLICT (cart_id, product_id) DO UPDATE SET quantity = LEAST(cart_items.quantity + EXCLUDED.quantity, {CartPricing.MaxLineQuantity})
                """, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>A repeated submit returns what the first one produced.</summary>
    private static IResult Replay(Order order) =>
        order is { Status: OrderStatus.Cancelled, CancellationReason: PaymentDeclined or PaymentError }
            ? Problem(StatusCodes.Status402PaymentRequired, "Your card was declined.", order.CancellationReason!)
            : Results.Ok(order.ToResponse());

    private Task<Order?> LoadOrderAsync(Guid userId, System.Linq.Expressions.Expression<Func<Order, bool>> predicate, CancellationToken cancellationToken) =>
        db.Orders.Include(o => o.Items).Include(o => o.StatusChanges)
            .Where(o => o.UserId == userId).Where(predicate)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

    private static IResult Problem(int status, string title, string code, Dictionary<string, object?>? extensions = null)
    {
        var all = new Dictionary<string, object?>(extensions ?? []) { ["code"] = code };
        var detail = all.Remove("detail", out var d) ? d as string : null;
        return Results.Problem(statusCode: status, title: title, detail: detail, extensions: all);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderNumber} placed and paid, total {Total}.")]
    private static partial void LogPlaced(ILogger logger, string orderNumber, decimal total);

    [LoggerMessage(Level = LogLevel.Information, Message = "Payment declined for order {OrderNumber}; order cancelled, stock and cart restored.")]
    private static partial void LogDeclined(ILogger logger, string orderNumber);

    [LoggerMessage(Level = LogLevel.Error, Message = "Payment gateway failed for order {OrderNumber}; order cancelled, stock and cart restored.")]
    private static partial void LogGatewayError(ILogger logger, Exception exception, string orderNumber);
}
