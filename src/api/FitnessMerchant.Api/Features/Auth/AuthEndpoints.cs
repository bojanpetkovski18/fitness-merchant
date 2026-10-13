using System.Security.Claims;
using FitnessMerchant.Api.Common;
using FitnessMerchant.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace FitnessMerchant.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IServiceCollection AddAuthFeature(this IServiceCollection services) => services
        .AddScoped<XsrfCookie>()
        .AddScoped<GetSessionQuery>()
        .AddScoped<RegisterHandler>()
        .AddScoped<SignInHandler>();

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous();

        auth.MapGet("/me", async (HttpContext context, GetSessionQuery query, XsrfCookie xsrf) =>
        {
            xsrf.Issue(context);
            return TypedResults.Ok(await query.ExecuteAsync(context.User));
        });

        auth.MapPost("/register", (RegisterRequest request, HttpContext context, RegisterHandler handler, CancellationToken ct) =>
                handler.ExecuteAsync(request, context, ct))
            .RequireRateLimiting(RateLimiting.AuthPolicy);

        auth.MapPost("/sign-in", (SignInRequest request, HttpContext context, SignInHandler handler) =>
                handler.ExecuteAsync(request, context))
            .RequireRateLimiting(RateLimiting.AuthPolicy);

        auth.MapPost("/sign-out", async (HttpContext context, SignInManager<AppUser> signIn, XsrfCookie xsrf) =>
        {
            await signIn.SignOutAsync();
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            xsrf.Issue(context);
            return TypedResults.NoContent();
        });

        return app;
    }
}
