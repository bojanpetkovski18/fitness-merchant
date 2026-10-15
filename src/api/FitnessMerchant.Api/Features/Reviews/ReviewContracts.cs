using FitnessMerchant.Api.Features.Catalog;

namespace FitnessMerchant.Api.Features.Reviews;

public sealed record CreateReviewRequest(int? Rating, string? Title, string? Body);

/// <summary>A public review: the author is a display name only, never an email (SPEC F11).</summary>
public sealed record ReviewResponse(Guid Id, int Rating, string Title, string Body, string Author, DateTimeOffset CreatedAt);

/// <summary><c>Distribution</c> has one count per star, 5 down to 1; only visible reviews count.</summary>
public sealed record RatingSummary(decimal Average, int Count, IReadOnlyList<int> Distribution);

public enum ReviewEligibility
{
    CanReview,
    SignIn,
    NotPurchased,
    AlreadyReviewed,
}

public sealed record ProductReviewsResponse(RatingSummary Summary, PagedResponse<ReviewResponse> Reviews, ReviewEligibility Viewer);
