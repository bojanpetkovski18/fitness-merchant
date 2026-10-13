namespace FitnessMerchant.Api.Features.Health;

public static class HealthEndpoints
{
    /// <summary>Liveness only for now; M02 adds the database readiness check and <c>/health/live</c>.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health");
        return app;
    }
}
