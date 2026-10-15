using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FitnessMerchant.Api.Features.Admin;
using FitnessMerchant.Api.Features.Catalog;
using FitnessMerchant.Api.IntegrationTests.Infrastructure;
using FitnessMerchant.Api.Infrastructure.Images;
using FitnessMerchant.Api.Tests.Images;
using Microsoft.AspNetCore.Http;

namespace FitnessMerchant.Api.IntegrationTests.Admin;

public class AdminImageTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_png_upload_is_stored_and_served_as_webp()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var admin = await factory.CreateAdminClientAsync();

        var response = await UploadAsync(admin, TestImages.Png(), "photo.png", "image/png");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var image = (await response.Content.ReadFromJsonAsync<AdminImageResponse>(Ct))!;
        Assert.Equal(("image/webp", 40, 20, $"/api/images/{image.Id}"), (image.ContentType, image.Width, image.Height, image.Url));
        Assert.Equal(image.Url, response.Headers.Location?.ToString());

        var served = await admin.Http.GetAsync(image.Url, Ct);
        Assert.Equal("image/webp", served.Content.Headers.ContentType?.MediaType);
        var bytes = await served.Content.ReadAsByteArrayAsync(Ct);
        Assert.Equal(UploadedImageProcessor.Format.Webp, UploadedImageProcessor.Sniff(bytes));
        Assert.Equal(image.SizeBytes, bytes.Length);
    }

    [Fact]
    public async Task Uploading_the_same_file_again_returns_the_same_image()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var admin = await factory.CreateAdminClientAsync();
        var jpeg = TestImages.Jpeg(64, 48);

        var first = await UploadAsync(admin, jpeg, "a.jpg", "image/jpeg");
        var second = await UploadAsync(admin, jpeg, "renamed-copy.jpg", "image/jpeg");

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK), (first.StatusCode, second.StatusCode));
        Assert.Equal(
            (await first.Content.ReadFromJsonAsync<AdminImageResponse>(Ct))!.Id,
            (await second.Content.ReadFromJsonAsync<AdminImageResponse>(Ct))!.Id);
    }

    [Fact]
    public async Task Exif_metadata_never_reaches_the_stored_image()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var admin = await factory.CreateAdminClientAsync();

        var response = await UploadAsync(admin, TestImages.JpegWithExif(), "camera.jpg", "image/jpeg");

        var image = (await response.Content.ReadFromJsonAsync<AdminImageResponse>(Ct))!;
        var stored = Encoding.ASCII.GetString(await admin.Http.GetByteArrayAsync(image.Url, Ct));
        Assert.DoesNotContain(TestImages.ExifCamera, stored, StringComparison.Ordinal);
        Assert.DoesNotContain("EXIF", stored, StringComparison.Ordinal);
        Assert.Equal((20, 40), (image.Width, image.Height)); // turned upright before the metadata was dropped
    }

    [Fact]
    public async Task A_text_file_with_a_spoofed_image_type_and_name_is_rejected()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var admin = await factory.CreateAdminClientAsync();

        var response = await UploadAsync(admin, "just some notes, not a picture"u8.ToArray(), "holiday.png", "image/png");

        Assert.Equal(["Upload a PNG, JPEG or WebP image."], (await ValidationErrorsAsync(response))["file"]);
        Assert.Equal(0, (await admin.Http.GetFromJsonAsync<PagedResponse<AdminImageResponse>>("/api/admin/images", Ct))!.Items.Count(i => i.ContentType == "image/webp"));
    }

    [Fact]
    public async Task An_image_over_4000_px_is_rejected()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var admin = await factory.CreateAdminClientAsync();

        var response = await UploadAsync(admin, TestImages.Png(4001, 8), "wide.png", "image/png");

        Assert.Contains("maximum is 4000×4000 px", (await ValidationErrorsAsync(response))["file"][0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_file_over_1_mb_gets_413()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var admin = await factory.CreateAdminClientAsync();
        var big = new byte[UploadedImageProcessor.MaxBytes + 1];
        TestImages.Png().AsSpan(0, 8).CopyTo(big);

        var response = await UploadAsync(admin, big, "big.png", "image/png");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_request_without_a_file_is_a_field_error()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var admin = await factory.CreateAdminClientAsync();

        var json = await admin.Http.PostAsJsonAsync("/api/admin/images", new { file = "x" }, Ct);
        using var emptyForm = new MultipartFormDataContent { { new StringContent("value"), "other" } };
        var noFile = await admin.Http.PostAsync("/api/admin/images", emptyForm, Ct);

        Assert.Equal(["Choose an image file to upload."], (await ValidationErrorsAsync(json))["file"]);
        Assert.Equal(["Choose an image file to upload."], (await ValidationErrorsAsync(noFile))["file"]);
    }

    [Fact]
    public async Task Uploads_need_the_antiforgery_header_and_are_rate_limited()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync(), new Dictionary<string, string?>
        {
            ["RateLimiting:UploadPermitLimit"] = "2",
        });
        using var admin = await factory.CreateAdminClientAsync();

        admin.SendXsrfHeader = false;
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(admin, TestImages.Png(), "a.png", "image/png")).StatusCode);
        admin.SendXsrfHeader = true;

        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(admin, TestImages.Png(10, 10), "a.png", "image/png")).StatusCode);
        var limited = await UploadAsync(admin, TestImages.Png(12, 12), "b.png", "image/png");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task The_picker_lists_images_newest_first()
    {
        await using var factory = new ApiFactory(await postgres.CreateDatabaseAsync());
        using var admin = await factory.CreateAdminClientAsync();
        var uploaded = (await (await UploadAsync(admin, TestImages.Webp(30, 30), "new.webp", "image/webp")).Content.ReadFromJsonAsync<AdminImageResponse>(Ct))!;

        var page = (await admin.Http.GetFromJsonAsync<PagedResponse<AdminImageResponse>>("/api/admin/images", Ct))!;

        Assert.Equal(uploaded.Id, page.Items[0].Id);
        Assert.Equal(ListAdminImagesQuery.PageSize, page.PageSize);
        Assert.True(page.TotalCount > 60); // the seeded artwork is there too
    }

    private static async Task<HttpResponseMessage> UploadAsync(BrowserClient client, byte[] data, string fileName, string contentType)
    {
        using var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var form = new MultipartFormDataContent { { file, "file", fileName } };
        return await client.Http.PostAsync("/api/admin/images", form, Ct);
    }

    private static async Task<IDictionary<string, string[]>> ValidationErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct))!.Errors;
    }
}
