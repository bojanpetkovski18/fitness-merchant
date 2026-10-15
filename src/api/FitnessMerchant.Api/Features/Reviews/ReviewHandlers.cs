using System.Security.Claims;
using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Domain.Orders;
using FitnessMerchant.Api.Domain.Reviews;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Reviews;

/// <summary>Who may review and how the product rating is kept in step with the reviews (D028).</summary>
public static class ReviewRules
{
    /// <summary>SPEC section 5: an order that was paid (Paid, Shipped or Delivered) containing the product.</summary>
    internal static readonly OrderStatus[] QualifyingStatuses = [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered];

    public static Task<bool> HasBoughtAsync(AppDbContext db, Guid userId, Guid productId, CancellationToken ct) =>
        db.Orders.AnyAsync(o => o.UserId == userId && QualifyingStatuses.Contains(o.Status) && o.Items.Any(i => i.ProductId == productId), ct);

    /// <summary>
    /// Locks the product row until the transaction ends. Two reviews of one product written at the same moment then
    /// run one after the other, and each recalculation (a new statement, so a new snapshot under READ COMMITTED)
    /// sees every committed review. Without it, both could compute the rating from a set missing the other's row.
    /// </summary>
    public static Task LockProductAsync(AppDbContext db, Guid productId, CancellationToken ct) =>
        db.Database.ExecuteSqlAsync($"SELECT 1 FROM products WHERE id = {productId} FOR UPDATE", ct);

    /// <summary>Average and count recomputed from the visible reviews by PostgreSQL, never adjusted in memory.</summary>
    public static Task RecalculateAsync(AppDbContext db, Guid productId, CancellationToken ct) =>
        db.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.RatingCount, p => db.Reviews.Count(r => r.ProductId == p.Id && !r.IsHidden))
            .SetProperty(p => p.RatingAverage, p => Math.Round(
                db.Reviews.Where(r => r.ProductId == p.Id && !r.IsHidden).Average(r => (decimal?)r.Rating) ?? 0m, 2)), ct);
}

public sealed class ListProductReviewsQuery(AppDbContext db)
{
    public const int PageSize = 5;

    public async Task<ProductReviewsResponse?> ExecuteAsync(string slug, int page, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Slug == slug)
            .Select(p => new { p.Id, p.RatingAverage, p.RatingCount })
            .SingleOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            return null;
        }

        var visible = db.Reviews.AsNoTracking().Where(r => r.ProductId == product.Id && !r.IsHidden);
        var counts = await visible.GroupBy(r => r.Rating).Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Rating, g => g.Count, cancellationToken);
        var total = counts.Values.Sum();
        var items = await visible
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Join(db.Users, r => r.UserId, u => u.Id, (r, u) => new ReviewResponse(r.Id, r.Rating, r.Title, r.Body, u.DisplayName, r.CreatedAt))
            .ToListAsync(cancellationToken);
        // The join may reorder rows; keep newest first.
        items = [.. items.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)];

        return new ProductReviewsResponse(
            new RatingSummary(product.RatingAverage, product.RatingCount, [.. Enumerable.Range(1, 5).Reverse().Select(star => counts.GetValueOrDefault(star))]),
            new PagedResponse<ReviewResponse>(items, page, PageSize, total),
            await EligibilityAsync(product.Id, user, cancellationToken));
    }

    private async Task<ReviewEligibility> EligibilityAsync(Guid productId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return ReviewEligibility.SignIn;
        }
        var userId = user.GetUserId();
        if (await db.Reviews.AnyAsync(r => r.ProductId == productId && r.UserId == userId, ct))
        {
            return ReviewEligibility.AlreadyReviewed;
        }
        return await ReviewRules.HasBoughtAsync(db, userId, productId, ct) ? ReviewEligibility.CanReview : ReviewEligibility.NotPurchased;
    }
}

public sealed partial class CreateReviewHandler(AppDbContext db, TimeProvider time, ILogger<CreateReviewHandler> logger)
{
    public async Task<IResult> ExecuteAsync(string slug, Guid userId, CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim() ?? "";
        var body = request.Body?.Trim() ?? "";
        var errors = new Dictionary<string, string[]>();
        if (request.Rating is not (>= Review.MinRating and <= Review.MaxRating))
        {
            errors["rating"] = ["Choose a rating from 1 to 5 stars."];
        }
        if (title.Length is < Review.TitleMin or > Review.TitleMax)
        {
            errors["title"] = [$"The title must be {Review.TitleMin} to {Review.TitleMax} characters."];
        }
        if (body.Length is < Review.BodyMin or > Review.BodyMax)
        {
            errors["body"] = [$"The review must be {Review.BodyMin} to {Review.BodyMax} characters."];
        }
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var productId = await db.Products.Where(p => p.IsActive && p.Slug == slug).Select(p => (Guid?)p.Id).SingleOrDefaultAsync(cancellationToken);
        if (productId is not { } id)
        {
            return Results.NotFound();
        }
        if (!await ReviewRules.HasBoughtAsync(db, userId, id, cancellationToken))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only customers who bought this product can review it.",
                extensions: new Dictionary<string, object?> { ["code"] = "not-eligible" });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ReviewRules.LockProductAsync(db, id, cancellationToken);
        var review = new Review
        {
            Id = Guid.CreateVersion7(time.GetUtcNow()),
            ProductId = id,
            UserId = userId,
            Rating = request.Rating!.Value,
            Title = title,
            Body = body,
            CreatedAt = time.GetUtcNow(),
        };
        db.Reviews.Add(review);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception, out _))
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "You have already reviewed this product.",
                extensions: new Dictionary<string, object?> { ["code"] = "already-reviewed" });
        }
        await ReviewRules.RecalculateAsync(db, id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogCreated(logger, review.Id, id, review.Rating);

        var author = await db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).SingleAsync(cancellationToken);
        return Results.Created($"/api/catalog/products/{slug}/reviews",
            new ReviewResponse(review.Id, review.Rating, review.Title, review.Body, author, review.CreatedAt));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Review {ReviewId} added to product {ProductId} with {Rating} stars")]
    private static partial void LogCreated(ILogger logger, Guid reviewId, Guid productId, int rating);
}
