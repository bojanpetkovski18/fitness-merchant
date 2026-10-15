using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace FitnessMerchant.Api.Common;

public sealed class RateLimitOptions
{
    public const string Section = "RateLimiting";

    /// <summary>Sign-in and register attempts per client IP per window (D021).</summary>
    public int AuthPermitLimit { get; init; } = 10;

    public int AuthWindowSeconds { get; init; } = 60;

    /// <summary>Guest cart quotes per client IP per minute; generous, it only guards against abuse.</summary>
    public int QuotePermitLimit { get; init; } = 120;

    /// <summary>Checkout attempts per client IP per minute (each may hit the payment gateway).</summary>
    public int CheckoutPermitLimit { get; init; } = 10;

    /// <summary>Admin image uploads per client IP per minute; each one decodes and re-encodes an image.</summary>
    public int UploadPermitLimit { get; init; } = 20;
}

public static class RateLimiting
{
    public const string AuthPolicy = "auth";
    public const string QuotePolicy = "cart-quote";
    public const string CheckoutPolicy = "checkout";
    public const string UploadPolicy = "upload";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitOptions.Section).Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests.",
                        Detail = "Please wait a moment and try again.",
                    },
                });
            };

            // Per client IP. Behind a reverse proxy this needs forwarded headers (configured with deployment, M14).
            limiter.AddPolicy(AuthPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.AuthPermitLimit,
                    Window = TimeSpan.FromSeconds(options.AuthWindowSeconds),
                    QueueLimit = 0,
                }));
            limiter.AddPolicy(QuotePolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.QuotePermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            limiter.AddPolicy(CheckoutPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.CheckoutPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            limiter.AddPolicy(UploadPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.UploadPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }
}
