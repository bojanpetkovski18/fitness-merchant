using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Images;

public sealed record ImageContent(string ContentType, byte[] Data);

public sealed class GetImageQuery(AppDbContext db)
{
    public Task<ImageContent?> ExecuteAsync(Guid id, CancellationToken cancellationToken) =>
        db.Images
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new ImageContent(i.ContentType, i.Data))
            .SingleOrDefaultAsync(cancellationToken);
}

public static class ImageEndpoints
{
    /// <summary>Public image bytes. M04 adds long-lived caching headers and an ETag.</summary>
    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/images/{id:guid}", async (Guid id, GetImageQuery query, CancellationToken cancellationToken) =>
                await query.ExecuteAsync(id, cancellationToken) is { } image
                    ? Results.File(image.Data, image.ContentType)
                    : Results.NotFound())
            .WithName("GetImage")
            .AllowAnonymous();

        return app;
    }
}
