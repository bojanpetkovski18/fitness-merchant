using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace FitnessMerchant.Api.Common;

public sealed class ReverseProxyOptions
{
    public const string Section = "ReverseProxy";

    /// <summary>Comma-separated IP addresses of the proxies in front of the API, e.g. "10.0.0.5".</summary>
    public string? KnownProxies { get; init; }

    /// <summary>Comma-separated CIDR ranges of trusted proxies, e.g. "10.0.0.0/8" (a platform's internal network).</summary>
    public string? KnownNetworks { get; init; }

    /// <summary>
    /// How many trusted hops to read from the right of X-Forwarded-For: 1 when the proxy appends only the client,
    /// 2 when it also appends its own address ("client, front-end"). Entries further left are client-supplied.
    /// </summary>
    public int ForwardLimit { get; init; } = 1;
}

/// <summary>
/// K5: behind a reverse proxy every request arrives from the proxy's address, so the per-IP rate limits would
/// lump all clients together. With trusted proxies configured, the client address and scheme are taken from
/// X-Forwarded-For/Proto, but only when the request really comes from one of them; nothing configured means the
/// headers are ignored, so a client cannot spoof its way past the limits.
/// </summary>
public static class ForwardedHeadersSetup
{
    public static IServiceCollection AddAppForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(ReverseProxyOptions.Section).Get<ReverseProxyOptions>() ?? new ReverseProxyOptions();
        var proxies = Split(settings.KnownProxies).Select(IPAddress.Parse).ToList();
        var networks = Split(settings.KnownNetworks).Select(System.Net.IPNetwork.Parse).ToList();
        if (settings.ForwardLimit is < 1 or > 3)
        {
            throw new InvalidOperationException("ReverseProxy:ForwardLimit must be 1, 2 or 3.");
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Only the trusted hops are read; anything a client prepends stays ignored.
            options.ForwardLimit = settings.ForwardLimit;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            proxies.ForEach(options.KnownProxies.Add);
            networks.ForEach(options.KnownIPNetworks.Add);
        });
        services.AddSingleton(new ForwardedHeadersEnabled(proxies.Count + networks.Count > 0));
        return services;
    }

    /// <summary>First in the pipeline, so everything after it (rate limiting, HTTPS checks, logs) sees the client.</summary>
    public static WebApplication UseAppForwardedHeaders(this WebApplication app)
    {
        if (app.Services.GetRequiredService<ForwardedHeadersEnabled>().Enabled)
        {
            app.UseForwardedHeaders();
        }
        return app;
    }

    private static string[] Split(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed record ForwardedHeadersEnabled(bool Enabled);
}
