using System.Security.Cryptography;
using FitnessMerchant.Api.Domain.Media;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.Infrastructure.Data;
using FitnessMerchant.Api.Infrastructure.Images;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

namespace FitnessMerchant.Api.Features.Admin;

/// <summary>
/// <c>POST /api/admin/images</c>, multipart with one <c>file</c> part. The form is read here rather than bound as
/// <see cref="IFormFile"/>: binding would require the framework antiforgery middleware, and opting out of it would
/// also switch off ours (AntiforgeryValidationMiddleware honours the same metadata).
/// </summary>
public sealed partial class UploadImageHandler(AppDbContext db, TimeProvider time, ILogger<UploadImageHandler> logger)
{
    /// <summary>The file limit plus room for the multipart framing.</summary>
    public const long MaxRequestBytes = UploadedImageProcessor.MaxBytes + (16 * 1024);

    public async Task<IResult> ExecuteAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxRequestBytes)
        {
            return TooLarge();
        }
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodyLimit)
        {
            bodyLimit.MaxRequestBodySize = MaxRequestBytes; // also stops chunked bodies that send no length
        }
        if (!request.HasFormContentType)
        {
            return FileError("Choose an image file to upload.");
        }

        request.HttpContext.Features.Set<IFormFeature>(new FormFeature(request, new FormOptions
        {
            MultipartBodyLengthLimit = MaxRequestBytes,
            ValueCountLimit = 8,
            MultipartHeadersCountLimit = 8,
        }));

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return TooLarge(); // a form limit was exceeded
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TooLarge();
        }

        if (form.Files.GetFile("file") is not { } file || file.Length == 0)
        {
            return FileError("Choose an image file to upload.");
        }
        if (file.Length > UploadedImageProcessor.MaxBytes)
        {
            return TooLarge();
        }

        var data = new byte[file.Length];
        await using (var stream = file.OpenReadStream())
        {
            await stream.ReadExactlyAsync(data, cancellationToken);
        }

        // The declared content type and file name are ignored on purpose; only the bytes decide (SPEC section 8).
        var processed = UploadedImageProcessor.Process(data);
        if (processed is ProcessedImage.Rejected rejected)
        {
            LogRejected(logger, file.Length, rejected.Message);
            return FileError(rejected.Message);
        }
        var accepted = (ProcessedImage.Accepted)processed;
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(accepted.Webp));

        if (await Find(sha256, cancellationToken) is { } existing)
        {
            return Results.Ok(existing);
        }

        var image = new Image
        {
            Id = Guid.CreateVersion7(time.GetUtcNow()),
            Sha256 = sha256,
            ContentType = UploadedImageProcessor.ContentType,
            Data = accepted.Webp,
            SizeBytes = accepted.Webp.Length,
            Width = accepted.Width,
            Height = accepted.Height,
            CreatedAt = time.GetUtcNow(),
        };
        db.Images.Add(image);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception, out _))
        {
            // The same picture was uploaded at the same moment; both callers get the one stored row.
            db.ChangeTracker.Clear();
            return Results.Ok(await Find(sha256, cancellationToken));
        }

        LogStored(logger, image.Id, file.Length, image.SizeBytes, image.Width, image.Height);
        var response = new AdminImageResponse(image.Id, CatalogMapping.ImageUrl(image.Id)!, image.ContentType, image.Width, image.Height, image.SizeBytes, image.CreatedAt);
        return Results.Created(response.Url, response);
    }

    private Task<AdminImageResponse?> Find(string sha256, CancellationToken cancellationToken) =>
        db.Images.AsNoTracking()
            .Where(i => i.Sha256 == sha256)
            .Select(i => new AdminImageResponse(i.Id, "/api/images/" + i.Id, i.ContentType, i.Width, i.Height, i.SizeBytes, i.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    private static IResult FileError(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] });

    private static IResult TooLarge() => Results.Problem(
        statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "The image is larger than 1 MB.",
        extensions: new Dictionary<string, object?> { ["code"] = "file-too-large" });

    [LoggerMessage(Level = LogLevel.Information, Message = "Upload rejected ({Bytes} bytes): {Reason}")]
    private static partial void LogRejected(ILogger logger, long bytes, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Image {ImageId} stored: {UploadBytes} bytes in, {StoredBytes} bytes WebP, {Width}x{Height}")]
    private static partial void LogStored(ILogger logger, Guid imageId, long uploadBytes, int storedBytes, int width, int height);
}

public sealed class ListAdminImagesQuery(AppDbContext db)
{
    public const int PageSize = 24;

    /// <summary>Newest first, for the image picker. Metadata only; the bytes come from <c>/api/images/{id}</c>.</summary>
    public async Task<PagedResponse<AdminImageResponse>> ExecuteAsync(int page, CancellationToken cancellationToken)
    {
        var total = await db.Images.CountAsync(cancellationToken);
        var items = await db.Images.AsNoTracking()
            .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(i => new AdminImageResponse(i.Id, "/api/images/" + i.Id, i.ContentType, i.Width, i.Height, i.SizeBytes, i.CreatedAt))
            .ToListAsync(cancellationToken);
        return new PagedResponse<AdminImageResponse>(items, page, PageSize, total);
    }
}
