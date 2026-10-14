using FitnessMerchant.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace FitnessMerchant.Api.Features.Images;

public sealed record ImageContent(string ContentType, byte[] Data, string Sha256);

public sealed class GetImageQuery(AppDbContext db)
{
    public Task<ImageContent?> ExecuteAsync(Guid id, CancellationToken cancellationToken) =>
        db.Images
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new ImageContent(i.ContentType, i.Data, i.Sha256))
            .SingleOrDefaultAsync(cancellationToken);
}

public static class ImageEndpoints
{
    /// <summary>
    /// Image bytes never change for an ID (a new upload is a new row), so found images are cacheable for a year
    /// and marked immutable; the content hash is a strong ETag, and If-None-Match gets a 304. 404s are not cached.
    /// </summary>
    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/images/{id:guid}", async (Guid id, GetImageQuery query, HttpContext context, CancellationToken cancellationToken) =>
            {
                if (await query.ExecuteAsync(id, cancellationToken) is not { } image)
                {
                    return Results.NotFound();
                }

                context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                return Results.File(image.Data, image.ContentType, entityTag: new EntityTagHeaderValue($"\"{image.Sha256}\""));
            })
            .WithName("GetImage")
            .AllowAnonymous();

        return app;
    }
}
