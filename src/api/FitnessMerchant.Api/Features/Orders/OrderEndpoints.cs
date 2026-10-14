using System.Security.Claims;
using FitnessMerchant.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace FitnessMerchant.Api.Features.Orders;

public static class OrderEndpoints
{
    public static IServiceCollection AddOrdersFeature(this IServiceCollection services) => services
        .AddScoped<CheckoutHandler>()
        .AddScoped<OrderQueries>()
        .AddScoped<OrderCommands>();

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/checkout", (
                CheckoutRequest request,
                [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
                ClaimsPrincipal user,
                CheckoutHandler handler,
                CancellationToken ct) => handler.ExecuteAsync(user.GetUserId(), idempotencyKey, request, ct))
            .WithTags("Checkout")
            .RequireAuthorization()
            .RequireRateLimiting(Common.RateLimiting.CheckoutPolicy);

        var orders = app.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization();

        orders.MapGet("", (int? page, ClaimsPrincipal user, OrderQueries queries, CancellationToken ct) =>
            queries.ListAsync(user.GetUserId(), Math.Clamp(page ?? 1, 1, 10_000), ct));

        orders.MapGet("/{number}", async (string number, ClaimsPrincipal user, OrderQueries queries, CancellationToken ct) =>
            await queries.GetAsync(user.GetUserId(), number, ct) is { } order ? Results.Ok(order) : Results.NotFound());

        orders.MapPost("/{number}/cancel", (string number, ClaimsPrincipal user, OrderCommands commands, CancellationToken ct) =>
            commands.CancelAsync(user.GetUserId(), number, ct));

        return app;
    }
}
