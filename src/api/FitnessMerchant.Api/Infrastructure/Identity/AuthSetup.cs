using FitnessMerchant.Api.Infrastructure.Data;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FitnessMerchant.Api.Infrastructure.Identity;

public static class AuthSetup
{
    public const string AdminPolicy = "Admin";
    public const string SessionCookieName = "fm_session";
    private const string AntiforgeryCookieName = "fm_af";

    public static WebApplicationBuilder AddAppAuth(this WebApplicationBuilder builder)
    {
        var development = builder.Environment.IsDevelopment();
        // Cookies are HTTPS-only outside Development. "Auth:RequireHttps=false" is only for running the production
        // image locally over plain http (docker compose on localhost, D032); never set it on a public deployment.
        var requireHttps = builder.Configuration.GetValue("Auth:RequireHttps", !development);
        builder.Services.AddSingleton(new CookieHttpsPolicy(requireHttps));

        // D032: the keys behind session and antiforgery cookies live in PostgreSQL, so sessions survive a container
        // restart and work on every instance. A fixed application name keeps them valid whatever the install path.
        var dataProtection = builder.Services.AddDataProtection().SetApplicationName("FitnessMerchant");
        dataProtection.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>, DatabaseKeyRepositorySetup>();
        // D034: on the deployment the keys are also encrypted with a certificate kept outside the database (a mounted
        // secret: certificate and private key in one PEM file), so a database leak alone cannot forge sessions.
        if (builder.Configuration["DataProtection:CertificatePemPath"] is { Length: > 0 } pemPath)
        {
            var certificate = X509Certificate2.CreateFromPemFile(pemPath);
            dataProtection.ProtectKeysWithCertificate(certificate).UnprotectKeysWithAnyCertificate(certificate);
        }

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
            // HTTPS-only unless Auth:RequireHttps is off (Development, local compose).
            options.Cookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;

            // An API answers 401/403 (rendered as problem details) instead of redirecting to a login page.
            // Replacing Events drops Identity's default security-stamp hook, so it is wired back explicitly.
            options.Events = new CookieAuthenticationEvents
            {
                OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync,
                OnRedirectToLogin = context => SetStatus(context.Response, StatusCodes.Status401Unauthorized),
                OnRedirectToAccessDenied = context => SetStatus(context.Response, StatusCodes.Status403Forbidden),
            };
        });

        // D025: check the security stamp on every request, so a password change ends other sessions at once
        // (one indexed user lookup per authenticated request; the default is every 30 minutes).
        builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

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
            options.Cookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
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

/// <summary>Whether auth cookies are HTTPS-only (set once in <see cref="AuthSetup"/>).</summary>
public sealed record CookieHttpsPolicy(bool RequireHttps);
