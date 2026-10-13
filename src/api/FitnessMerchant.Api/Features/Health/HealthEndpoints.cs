namespace FitnessMerchant.Api.Features.Health;

public static class HealthEndpoints
{
    /// <summary>
    /// <c>/health/live</c>: the process is up (no dependencies checked).
    /// <c>/health</c>: ready to serve, including the database; 503 when a check fails.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health").AllowAnonymous();
        return app;
    }
}
