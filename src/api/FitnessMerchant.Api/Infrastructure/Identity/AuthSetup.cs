using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace FitnessMerchant.Api.Infrastructure.Identity;

public static class AuthSetup
{
    public const string AdminPolicy = "Admin";
    public const string SessionCookieName = "fm_session";
    private const string AntiforgeryCookieName = "fm_af";

    public static WebApplicationBuilder AddAppAuth(this WebApplicationBuilder builder)
    {
        var development = builder.Environment.IsDevelopment();

        builder.Services
            .AddIdentityCore<AppUser>(options =>
            {
                // SPEC section 5: at least 10 characters, Identity's character-class defaults otherwise.
                options.Password.RequiredLength = 10;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        builder.Services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = SessionCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            // Development runs over plain http; everywhere else the cookie is HTTPS-only.
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;

            // An API answers 401/403 (rendered as problem details) instead of redirecting to a login page.
            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = context => SetStatus(context.Response, StatusCodes.Status401Unauthorized),
                OnRedirectToAccessDenied = context => SetStatus(context.Response, StatusCodes.Status403Forbidden),
            };
        });

        builder.Services.AddAuthorizationBuilder()
            // Every endpoint requires a signed-in user unless it opts out with AllowAnonymous().
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AdminPolicy, policy => policy.RequireRole(Roles.Admin));

        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });

        return builder;
    }

    public static WebApplication UseAppAuth(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<AntiforgeryValidationMiddleware>();
        return app;
    }

    private static Task SetStatus(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
