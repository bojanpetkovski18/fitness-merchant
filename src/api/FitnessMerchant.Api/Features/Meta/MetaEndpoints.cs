using FitnessMerchant.Api.Features.Admin;

namespace FitnessMerchant.Api.Features.Meta;

/// <summary>What the SPA needs to know about this deployment, e.g. to show the demo banner. Public, no secrets.</summary>
public sealed record MetaResponse(bool DemoMode, int? ResetIntervalHours);

public static class MetaEndpoints
{
    public static IEndpointRouteBuilder MapMetaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/meta", (DemoOptions demo) =>
                new MetaResponse(demo.Enabled, demo.Enabled && demo.ResetIntervalHours > 0 ? demo.ResetIntervalHours : null))
            .WithTags("Meta")
            .AllowAnonymous();
        return app;
    }
}
