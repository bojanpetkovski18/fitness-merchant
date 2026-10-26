namespace FitnessMerchant.Api.Common;

/// <summary>
/// Adds the security headers from SPEC section 9 to every response. The production SPA build runs under this
/// CSP with no violations (K1, M12): no critical-CSS inlining, and no inline style from the CDK media matcher.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    /// <summary>
    /// D036: the Angular CDK adds three small &lt;style&gt; blocks the first time an overlay, dialog or
    /// screen-reader-only text appears. They are allowed by hash, so exactly these bytes and nothing else run as
    /// inline style. The hashes belong to @angular/cdk 22.2.0; after a CDK update the E2E journey fails on a CSP
    /// violation, and its message names the new hash.
    /// </summary>
    internal const string CdkStyleHashes =
        "'sha256-CcrXDjmowgW29Hlg/uZ8h05hSs8VRqliv/k7QWU4Yb0=' " + // .cdk-overlay-container, .cdk-global-overlay-wrapper
        "'sha256-UssxMTUZa7g+WrWTxbVzFpE3o3lEbjO69XA8YUVYbJ4=' " + // .cdk-dialog-container
        "'sha256-5Aa95/ZO6hntqOK1ZYDkfF0M76NFmrG+N39v6gp+12k='"; // .cdk-visually-hidden

    internal const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' " + CdkStyleHashes + "; img-src 'self' data:; font-src 'self'; " +
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
