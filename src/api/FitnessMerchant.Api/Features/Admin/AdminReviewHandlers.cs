using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.Features.Reviews;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Admin;

/// <summary>Admins see who wrote a review (email included) to deal with abuse; the public API never shows emails.</summary>
public sealed record AdminReviewRow(
    Guid Id,
    string ProductName,
    string ProductSlug,
    int Rating,
    string Title,
    string Body,
    string Author,
    string AuthorEmail,
    bool IsHidden,
    DateTimeOffset CreatedAt);

public sealed record ReviewVisibilityRequest(bool? IsHidden);

public sealed class ListAdminReviewsQuery(AppDbContext db)
{
    public const int PageSize = 20;

    public async Task<IResult> ExecuteAsync(string? status, int? page, CancellationToken cancellationToken)
    {
        var reviews = db.Reviews.AsNoTracking();
        switch (status)
        {
            case null or "" or "all":
                break;
            case "visible":
                reviews = reviews.Where(r => !r.IsHidden);
                break;
            case "hidden":
                reviews = reviews.Where(r => r.IsHidden);
                break;
            default:
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Status must be all, visible or hidden."] });
        }
        var pageNumber = Math.Clamp(page ?? 1, 1, 10_000);

        var total = await reviews.CountAsync(cancellationToken);
        var rows = await (
                from r in reviews
                join p in db.Products on r.ProductId equals p.Id
                join u in db.Users on r.UserId equals u.Id
                orderby r.CreatedAt descending, r.Id descending
                select new AdminReviewRow(r.Id, p.Name, p.Slug, r.Rating, r.Title, r.Body, u.DisplayName, u.Email!, r.IsHidden, r.CreatedAt))
            .Skip((pageNumber - 1) * PageSize).Take(PageSize)
            .ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<AdminReviewRow>(rows, pageNumber, PageSize, total));
    }
}

public sealed partial class SetReviewVisibilityHandler(AppDbContext db, ILogger<SetReviewVisibilityHandler> logger)
{
    public async Task<IResult> ExecuteAsync(Guid id, Guid adminId, ReviewVisibilityRequest request, CancellationToken cancellationToken)
    {
        if (request.IsHidden is not { } hidden)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["isHidden"] = ["Say whether the review is hidden."] });
        }
        var productId = await db.Reviews.Where(r => r.Id == id).Select(r => (Guid?)r.ProductId).SingleOrDefaultAsync(cancellationToken);
        if (productId is not { } product)
        {
            return Results.NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ReviewRules.LockProductAsync(db, product, cancellationToken);
        await db.Reviews.Where(r => r.Id == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsHidden, hidden), cancellationToken);
        await ReviewRules.RecalculateAsync(db, product, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogVisibility(logger, id, hidden, adminId);

        var row = await (
                from r in db.Reviews.AsNoTracking().Where(r => r.Id == id)
                join p in db.Products on r.ProductId equals p.Id
                join u in db.Users on r.UserId equals u.Id
                select new AdminReviewRow(r.Id, p.Name, p.Slug, r.Rating, r.Title, r.Body, u.DisplayName, u.Email!, r.IsHidden, r.CreatedAt))
            .SingleAsync(cancellationToken);
        return Results.Ok(row);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Review {ReviewId} hidden={Hidden} by admin {AdminId}")]
    private static partial void LogVisibility(ILogger logger, Guid reviewId, bool hidden, Guid adminId);
}
