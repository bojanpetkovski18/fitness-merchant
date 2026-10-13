namespace FitnessMerchant.Api.Common;

/// <summary>
/// Adds the security headers from SPEC section 9 to every response. The CSP already fits the SPA
/// this API will serve (D008); it is re-verified against the production build in M13.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    internal const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
        "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    internal const string PermissionsPolicy =
        "camera=(), microphone=(), geolocation=(), usb=()";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = PermissionsPolicy;
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            // The Scalar API explorer (Development only) needs inline scripts, so it is exempt.
            if (!(environment.IsDevelopment() && IsApiExplorerPath(context.Request.Path)))
            {
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
            }

            return Task.CompletedTask;
        });

        return next(context);
    }

    private static bool IsApiExplorerPath(PathString path) =>
        path.StartsWithSegments("/scalar") || path.StartsWithSegments("/openapi");
}
