using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.Features.Orders;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Admin;

/// <summary>Raw query string of <c>GET /api/admin/orders</c>.</summary>
public sealed class AdminOrderListParameters
{
    public string? Status { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public string? Q { get; init; }
    public int? Page { get; init; }
}

internal static class AdminOrderFilters
{
    /// <summary>The kebab-case names the JSON API uses, e.g. "pending-payment".</summary>
    public static readonly IReadOnlyDictionary<string, OrderStatus> StatusKeys =
        Enum.GetValues<OrderStatus>().ToDictionary(s => System.Text.Json.JsonNamingPolicy.KebabCaseLower.ConvertName(s.ToString()), s => s, StringComparer.Ordinal);

    public static string LikePattern(string search) =>
        $"%{search.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal)}%";
}

public sealed class ListAdminOrdersQuery(AppDbContext db, TimeProvider time)
{
    public const int PageSize = 20;

    public async Task<IResult> ExecuteAsync(AdminOrderListParameters parameters, CancellationToken cancellationToken)
    {
        var errors = new FieldErrors();
        OrderStatus? status = null;
        if (parameters.Status is { Length: > 0 } s)
        {
            if (AdminOrderFilters.StatusKeys.TryGetValue(s, out var parsed))
            {
                status = parsed;
            }
            else
            {
                errors.Add("status", $"Status must be one of: {string.Join(", ", AdminOrderFilters.StatusKeys.Keys)}.");
            }
        }

        // No dates means all orders; either date turns the range on (the other defaults as in DateRange).
        DateRange? range = null;
        if (!string.IsNullOrWhiteSpace(parameters.From) || !string.IsNullOrWhiteSpace(parameters.To))
        {
            (range, var rangeErrors) = DateRange.Parse(parameters.From, parameters.To, time.GetUtcNow());
            foreach (var (field, messages) in rangeErrors.ToDictionary())
            {
                foreach (var message in messages)
                {
                    errors.Add(field, message);
                }
            }
        }

        var search = parameters.Q?.Trim() is { Length: > 0 } q ? q : null;
        if (search is { Length: > 100 })
        {
            errors.Add("q", "Search is limited to 100 characters.");
        }
        var page = parameters.Page ?? 1;
        if (page is < 1 or > 10_000)
        {
            errors.Add("page", "Page must be between 1 and 10000.");
        }
        if (errors.Any)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }

        var orders = db.Orders.AsNoTracking();
        if (status is { } st)
        {
            orders = orders.Where(o => o.Status == st);
        }
        if (range is not null)
        {
            orders = orders.Where(o => o.PlacedAt >= range.Start && o.PlacedAt < range.EndExclusive);
        }
        if (search is not null)
        {
            var pattern = AdminOrderFilters.LikePattern(search);
            orders = orders.Where(o =>
                EF.Functions.ILike(o.Number, pattern, @"\") ||
                EF.Functions.ILike(o.ContactEmail, pattern, @"\") ||
                db.Users.Any(u => u.Id == o.UserId && EF.Functions.ILike(u.Email!, pattern, @"\")));
        }

        var total = await orders.CountAsync(cancellationToken);
        var rows = await orders
            .OrderByDescending(o => o.PlacedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(o => new AdminOrderRow(o.Number, o.Status, o.PlacedAt, o.Shipping.FullName, o.ContactEmail, o.Items.Sum(i => i.Quantity), o.Total))
            .ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<AdminOrderRow>(rows, page, PageSize, total));
    }
}

public sealed class GetAdminOrderQuery(AppDbContext db)
{
    public async Task<AdminOrderResponse?> ExecuteAsync(string number, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).Include(o => o.StatusChanges)
            .AsSplitQuery()
            .SingleOrDefaultAsync(o => o.Number == number, cancellationToken);
        return order is null ? null : await ToResponseAsync(db, order, cancellationToken);
    }

    internal static async Task<AdminOrderResponse> ToResponseAsync(AppDbContext db, Order order, CancellationToken cancellationToken)
    {
        // One query for the customer and everyone who changed the status.
        var userIds = order.StatusChanges.Where(c => c.ChangedByUserId is not null).Select(c => c.ChangedByUserId!.Value).Append(order.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (Email: u.Email ?? "", u.DisplayName), cancellationToken);
        var customer = users.TryGetValue(order.UserId, out var c) ? new AdminCustomer(order.UserId, c.Email, c.DisplayName) : new AdminCustomer(order.UserId, "", "Unknown");

        var history = order.StatusChanges
            .OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
            .Select(h => new AdminStatusChange(h.FromStatus, h.ToStatus, h.ChangedAt, h.Note,
                h.ChangedByUserId is { } by ? (users.TryGetValue(by, out var u) ? u.DisplayName : "Unknown user") : null))
            .ToList();

        return new AdminOrderResponse(
            order.ToResponse(await OrderQueries.LiveSlugsAsync(db, order, cancellationToken)),
            customer,
            OrderStateMachine.NextStatuses(order.Status),
            history);
    }
}

/// <summary>
/// Moves an order along the state machine on behalf of an admin, recording who did it. Cancelling restocks in the
/// same transaction. A move that is not allowed from the current status (for example because another admin got
/// there first) is a 409 with the moves that are allowed now.
/// </summary>
public sealed partial class ChangeOrderStatusHandler(AppDbContext db, TimeProvider time, ILogger<ChangeOrderStatusHandler> logger)
{
    public const int MaxNoteLength = 500;

    public async Task<IResult> ExecuteAsync(string number, Guid adminId, ChangeStatusRequest request, CancellationToken cancellationToken)
    {
        var note = request.Note?.Trim() is { Length: > 0 } n ? n : null;
        var errors = new FieldErrors();
        if (request.ToStatus is null)
        {
            errors.Add("toStatus", "Choose the new status.");
        }
        if (note is { Length: > MaxNoteLength })
        {
            errors.Add("note", $"The note can be at most {MaxNoteLength} characters.");
        }
        if (errors.Any)
        {
            return Results.ValidationProblem(errors.ToDictionary());
        }

        var order = await db.Orders
            .Include(o => o.Items).Include(o => o.StatusChanges)
            .AsSplitQuery()
            .SingleOrDefaultAsync(o => o.Number == number, cancellationToken);
        if (order is null)
        {
            return Results.NotFound();
        }

        var from = order.Status;
        var to = request.ToStatus!.Value;
        if (!OrderStateMachine.CanTransition(from, to))
        {
            return AdminProblems.Conflict("invalid-transition", $"An order that is {Label(from)} cannot become {Label(to)}.",
                new Dictionary<string, object?> { ["allowedTransitions"] = OrderStateMachine.NextStatuses(from) });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = time.GetUtcNow();
        if (to == OrderStatus.Cancelled)
        {
            order.Cancel("admin", now, adminId, note);
            foreach (var item in order.Items)
            {
                await StockLedger.ReturnAsync(db, item.ProductId, item.Quantity, now, cancellationToken);
            }
        }
        else
        {
            order.TransitionTo(to, now, adminId, note);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Changed meanwhile (the customer cancelled, another admin moved it); the restock rolls back too.
            return AdminProblems.Conflict("concurrent-change", "This order changed meanwhile. Reload it and try again.");
        }
        await transaction.CommitAsync(cancellationToken);
        LogStatusChanged(logger, order.Number, from, to, adminId);

        return Results.Ok(await GetAdminOrderQuery.ToResponseAsync(db, order, cancellationToken));
    }

    private static string Label(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => "awaiting payment",
        _ => status.ToString().ToLowerInvariant(),
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderNumber} moved from {From} to {To} by admin {AdminId}")]
    private static partial void LogStatusChanged(ILogger logger, string orderNumber, OrderStatus from, OrderStatus to, Guid adminId);
}
