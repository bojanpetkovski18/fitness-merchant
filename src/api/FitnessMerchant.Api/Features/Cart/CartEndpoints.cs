using System.Security.Claims;
using FitnessMerchant.Api.Common;

namespace FitnessMerchant.Api.Features.Cart;

public static class CartEndpoints
{
    public static IServiceCollection AddCartFeature(this IServiceCollection services) => services
        .AddScoped<CartPricer>()
        .AddScoped<CartQueries>()
        .AddScoped<CartCommands>();

    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        // The caller's own cart only; there is no cart ID anywhere in these routes.
        var cart = app.MapGroup("/api/cart").WithTags("Cart").RequireAuthorization();

        cart.MapGet("", (ClaimsPrincipal user, CartQueries queries, CancellationToken ct) =>
            queries.GetAsync(user.GetUserId(), ct));

        cart.MapPut("/items/{productId:guid}", (Guid productId, SetQuantityRequest request, ClaimsPrincipal user, CartCommands commands, CancellationToken ct) =>
            commands.SetQuantityAsync(user.GetUserId(), productId, request.Quantity, ct));

        cart.MapDelete("/items/{productId:guid}", (Guid productId, ClaimsPrincipal user, CartCommands commands, CancellationToken ct) =>
            commands.RemoveAsync(user.GetUserId(), productId, ct));

        cart.MapDelete("", (ClaimsPrincipal user, CartCommands commands, CancellationToken ct) =>
            commands.ClearAsync(user.GetUserId(), ct));

        cart.MapPost("/merge", (CartItemsRequest request, ClaimsPrincipal user, CartCommands commands, CancellationToken ct) =>
            commands.MergeAsync(user.GetUserId(), request, ct));

        // Guests: real server prices for a browser-held cart, nothing stored.
        cart.MapPost("/quote", (CartItemsRequest request, CartQueries queries, CancellationToken ct) =>
                queries.QuoteAsync(request, ct))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiting.QuotePolicy);

        return app;
    }
}
