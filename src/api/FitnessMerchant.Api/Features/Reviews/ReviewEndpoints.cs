using System.Security.Claims;
using FitnessMerchant.Api.Common;

namespace FitnessMerchant.Api.Features.Reviews;

public static class ReviewEndpoints
{
    public static IServiceCollection AddReviewsFeature(this IServiceCollection services) => services
        .AddScoped<ListProductReviewsQuery>()
        .AddScoped<CreateReviewHandler>();

    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var reviews = app.MapGroup("/api/catalog/products/{slug}/reviews").WithTags("Reviews");

        // Public; a signed-in caller also learns whether they may review (the cookie is read when present).
        reviews.MapGet("", async (string slug, int? page, ClaimsPrincipal user, ListProductReviewsQuery query, CancellationToken ct) =>
                await query.ExecuteAsync(slug, Math.Clamp(page ?? 1, 1, 10_000), user, ct) is { } result ? Results.Ok(result) : Results.NotFound())
            .AllowAnonymous();

        reviews.MapPost("", (string slug, CreateReviewRequest request, ClaimsPrincipal user, CreateReviewHandler handler, CancellationToken ct) =>
                handler.ExecuteAsync(slug, user.GetUserId(), request, ct))
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiting.ReviewPolicy);

        return app;
    }
}
