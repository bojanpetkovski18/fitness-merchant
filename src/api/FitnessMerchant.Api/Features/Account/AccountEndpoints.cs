using System.Security.Claims;
using FitnessMerchant.Api.Common;

namespace FitnessMerchant.Api.Features.Account;

public static class AccountEndpoints
{
    public static IServiceCollection AddAccountFeature(this IServiceCollection services) => services
        .AddScoped<UpdateProfileHandler>()
        .AddScoped<ChangePasswordHandler>();

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/account").WithTags("Account").RequireAuthorization();

        account.MapPut("/profile", (UpdateProfileRequest request, ClaimsPrincipal user, UpdateProfileHandler handler) =>
            handler.ExecuteAsync(request, user));

        // Shares the sign-in budget: guessing the current password is the same attack as guessing it at sign-in.
        account.MapPost("/password", (ChangePasswordRequest request, HttpContext context, ChangePasswordHandler handler) =>
                handler.ExecuteAsync(request, context))
            .RequireRateLimiting(RateLimiting.AuthPolicy);

        return app;
    }
}
