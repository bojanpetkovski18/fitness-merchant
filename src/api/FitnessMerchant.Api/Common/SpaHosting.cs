using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace FitnessMerchant.Api.Common;

public sealed class SpaOptions
{
    public const string Section = "Spa";

    /// <summary>
    /// The Angular production build (<c>dist/web/browser</c>); relative paths resolve from the content root.
    /// Empty or missing: the API serves no SPA (development uses the Angular dev server and its proxy).
    /// </summary>
    public string? RootPath { get; init; }
}

/// <summary>
/// Serves the built SPA from the same origin as the API, so the strict CSP and SameSite cookies hold (D008).
/// Hashed bundles are immutable for a year; index.html is never cached, so a deploy shows up on the next load.
/// Deep links (/products/x) get index.html; /api and /health never do, so unknown API routes keep their
/// 401/404 problem details instead of returning a page.
/// </summary>
public static partial class SpaHosting
{
    public static IServiceCollection AddAppSpa(this IServiceCollection services)
    {
        services.Configure<RouteOptions>(options => options.SetParameterPolicy<SpaRouteConstraint>(SpaRouteConstraint.Name));
        services.AddResponseCompression(options =>
        {
            // Only the SPA's own files go through this (see UseAppSpaFiles), never API responses: compressing
            // responses that mix user data and secrets over HTTPS invites BREACH-style attacks.
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ["text/html", "text/css", "text/javascript", "application/javascript", "image/svg+xml"];
        });
        // On the fly only for files without a precompressed copy (index.html, a few SVGs): small, so Optimal is cheap.
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);
        return services;
    }

    public static WebApplication UseAppSpaFiles(this WebApplication app)
    {
        if (Provider(app) is not { } files)
        {
            return app;
        }

        // M12 Lighthouse: the bundles went out uncompressed. The build writes .br/.gz copies at maximum
        // compression (scripts/precompress.mjs); serve those when the browser takes them. Anything without a
        // copy (index.html from the fallback) is compressed on the fly. Never under /api or /health.
        app.UseWhen(
            context => !context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/health"),
            branch =>
            {
                branch.Use((context, next) =>
                {
                    ServePrecompressed(context, files);
                    return next(context);
                });
                branch.UseResponseCompression();
            });

        // Before authentication: static files are public and are not endpoints, so the fallback policy does not apply.
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ContentTypeProvider = new PrecompressedContentTypes(),
            OnPrepareResponse = context => SetCaching(context.Context.Response, context.File.Name),
        });
        return app;
    }

    public static WebApplication MapAppSpaFallback(this WebApplication app)
    {
        if (Provider(app) is not { } files)
        {
            return app;
        }

        app.MapFallbackToFile("{*path:nonfile:spa}", "index.html", new StaticFileOptions
        {
            FileProvider = files,
            OnPrepareResponse = context => SetCaching(context.Context.Response, context.File.Name),
        }).AllowAnonymous();
        return app;
    }

    /// <summary>Points the request at "x.js.br" (or ".gz") when that copy exists and the client accepts it.</summary>
    private static void ServePrecompressed(HttpContext context, PhysicalFileProvider files)
    {
        var path = context.Request.Path.Value;
        if (!(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) ||
            string.IsNullOrEmpty(path) || !files.GetFileInfo(path).Exists)
        {
            return;
        }

        var accepted = context.Request.GetTypedHeaders().AcceptEncoding;
        foreach (var (encoding, suffix) in new[] { ("br", ".br"), ("gzip", ".gz") })
        {
            if (accepted.Any(a => a.Value.Equals(encoding, StringComparison.OrdinalIgnoreCase) && (a.Quality ?? 1) > 0) &&
                files.GetFileInfo(path + suffix).Exists)
            {
                context.Request.Path = path + suffix;
                context.Response.Headers.ContentEncoding = encoding;
                context.Response.Headers.Vary = HeaderNames.AcceptEncoding;
                return;
            }
        }
    }

    internal static void SetCaching(HttpResponse response, string fileName)
    {
        response.Headers.CacheControl = fileName == "index.html"
            ? "no-cache"
            : HashedName().IsMatch(fileName) ? "public, max-age=31536000, immutable" : "public, max-age=3600";
    }

    private static PhysicalFileProvider? Provider(WebApplication app)
    {
        var root = app.Configuration.GetSection(SpaOptions.Section).Get<SpaOptions>()?.RootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }
        var full = Path.GetFullPath(root, app.Environment.ContentRootPath);
        return File.Exists(Path.Combine(full, "index.html")) ? new PhysicalFileProvider(full) : null;
    }

    /// <summary>Angular's output hashing: "main-I64NK2YK.js", "chunk-B29o-tf5.js", "styles-5INURTSO.css".</summary>
    [GeneratedRegex(@"-[A-Za-z0-9_-]{8}\.(js|css)(\.br|\.gz)?$")]
    private static partial Regex HashedName();
}

/// <summary>Route constraint "spa": every path except /api and /health (and their sub-paths) is a page of the SPA.</summary>
public sealed class SpaRouteConstraint : IRouteConstraint
{
    public const string Name = "spa";

    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        var path = values.TryGetValue(routeKey, out var value) ? value?.ToString() ?? "" : "";
        return !(IsUnder(path, "api") || IsUnder(path, "health"));
    }

    private static bool IsUnder(string path, string segment) =>
        path.Equals(segment, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(segment + "/", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Content types for precompressed copies: "main.js.br" is still JavaScript.</summary>
internal sealed class PrecompressedContentTypes : IContentTypeProvider
{
    private readonly FileExtensionContentTypeProvider _types = new();

    public bool TryGetContentType(string subpath, out string contentType) =>
        _types.TryGetContentType(
            subpath.EndsWith(".br", StringComparison.Ordinal) || subpath.EndsWith(".gz", StringComparison.Ordinal) ? subpath[..^3] : subpath,
            out contentType!);
}
